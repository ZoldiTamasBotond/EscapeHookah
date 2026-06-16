using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EscapeHookah.Shared.Models;
using Firebase.Database;
using Firebase.Database.Query;

namespace EscapeHookah.Shared.Services
{
    public class ReservationService : IReservationService
    {
        private readonly FirebaseClient _databaseClient;
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly List<Table> _tables;
        private readonly TimeSpan _holdDuration = TimeSpan.FromMinutes(15);

        public ReservationService(IFirebaseAuthService firebaseAuthService)
        {
            _firebaseAuthService = firebaseAuthService;

            // initialize database client lazily to avoid token refresh issues
            _databaseClient = null;

            try
            {
                _databaseClient = new FirebaseClient(
                    "https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/",
                    new FirebaseOptions
                    {
                        AuthTokenAsyncFactory = async () => await _firebaseAuthService.GetIdTokenAsync()
                    });
            }
            catch
            {
                // will attempt later
            }

            _tables = TableLayoutDefinitions.AllTables;
        }

        public async Task<List<Reservation>> GetAllReservations()
        {
            try
            {
                var list = await FetchAllReservationsFromDatabaseAsync();
                await CancelExpiredReservations(list);
                return list.Where(r => !IsHistorical(r)).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting all reservations: {ex.Message}");
                return new List<Reservation>();
            }
        }

        public Task<List<Table>> GetTables()
        {
            return Task.FromResult(_tables);
        }

        public async Task<List<Reservation>> GetUserReservations(string userId)
        {
            try
            {
                var list = await FetchAllReservationsFromDatabaseAsync();
                list = list.Where(r => r.UserId == userId).ToList();

                await CancelExpiredReservations(list);

                return list.Where(r => !IsHistorical(r)).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user reservations: {ex.Message}");
                return new List<Reservation>();
            }
        }

        public async Task<List<Reservation>> GetUserReservationHistory(string userId)
        {
            try
            {
                var list = await FetchAllReservationsFromDatabaseAsync();
                list = list.Where(r => r.UserId == userId).ToList();

                await CancelExpiredReservations(list);

                return list.Where(r => IsHistorical(r)).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user reservation history: {ex.Message}");
                return new List<Reservation>();
            }
        }

        public async Task<List<Reservation>> GetAllReservationHistory()
        {
            try
            {
                var list = await FetchAllReservationsFromDatabaseAsync();
                await CancelExpiredReservations(list);
                return list.Where(r => IsHistorical(r)).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting all reservation history: {ex.Message}");
                return new List<Reservation>();
            }
        }

        public async Task<List<Reservation>> GetReservationsByDate(DateTime date)
        {
            try
            {
                var reservations = await _databaseClient
                    .Child("reservations")
                    .OnceAsync<Reservation>();

                var list = reservations
                    .Where(r => r.Object != null)
                    .Select(r => MapFirebaseReservation(r.Key, r.Object))
                    .ToList();

                // Mark expired pending reservations as cancelled
                await CancelExpiredReservations(list);

                return list
                    .Where(r => r.ReservationDate.Date == date.Date && r.Status != ReservationStatus.Cancelled)
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting reservations by date: {ex.Message}");
                return new List<Reservation>();
            }
        }

        public async Task<bool> IsTableAvailable(int tableNumber, DateTime date, TimeSpan startTime, TimeSpan endTime)
        {
            try
            {
                var reservations = await GetReservationsByDate(date);

                return !reservations.Any(r =>
                    r.TableNumber == tableNumber &&
                    r.Status != ReservationStatus.Cancelled &&
                    (
                        (startTime >= r.StartTime && startTime < r.EndTime) ||
                        (endTime > r.StartTime && endTime <= r.EndTime) ||
                        (startTime <= r.StartTime && endTime >= r.EndTime)
                    ));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking table availability: {ex.Message}");
                return true; // Assume available if error
            }
        }

        public async Task<Reservation?> CreateReservation(Reservation reservation)
        {
            try
            {
                if (!_firebaseAuthService.IsAuthenticated)
                    throw new Exception("User is not authenticated.");

                if (string.IsNullOrWhiteSpace(_firebaseAuthService.CurrentUserId))
                    throw new Exception("Authenticated user ID is missing.");

                if (reservation.UserId != _firebaseAuthService.CurrentUserId)
                    throw new Exception("Reservation user ID does not match authenticated user.");

                // Check availability using current rules (expired pending reservations are cleaned up in GetReservationsByDate)
                var isAvailable = await IsTableAvailable(
                    reservation.TableNumber,
                    reservation.ReservationDate,
                    reservation.StartTime,
                    reservation.EndTime);

                if (!isAvailable)
                    throw new Exception("Table is not available at the selected time");

                reservation.CreatedAt = DateTime.UtcNow;
                reservation.ReservationDate = reservation.ReservationDate.Date;
                reservation.UserName ??= string.Empty;
                reservation.SpecialRequests ??= string.Empty;

                // If creating a hold, set expiration
                if (reservation.Status == ReservationStatus.Pending)
                {
                    reservation.ExpiresAt = DateTime.UtcNow.Add(_holdDuration);
                }

                // Default to Confirmed if not specified
                if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
                    reservation.Status = ReservationStatus.Confirmed;

                var result = await _databaseClient
                    .Child("reservations")
                    .PostAsync(reservation);

                reservation.Id = result.Key;
                return reservation;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating reservation: {ex.Message}");
                throw;
            }
        }

        public async Task<Reservation?> GetReservationById(string reservationId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reservationId))
                    return null;

                var reservation = await _databaseClient
                    .Child("reservations")
                    .Child(reservationId)
                    .OnceSingleAsync<Reservation>();

                if (reservation == null)
                    return null;

                // Map and handle expiry if needed
                var mapped = MapFirebaseReservation(reservationId, reservation);
                if (mapped.Status == ReservationStatus.Pending && mapped.ExpiresAt.HasValue && mapped.ExpiresAt.Value.ToUniversalTime() < DateTime.UtcNow)
                {
                    // Mark cancelled
                    mapped.Status = ReservationStatus.Cancelled;
                    await UpdateReservation(mapped);
                    return null;
                }

                return mapped;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting reservation by id: {ex.Message}");
                return null;
            }
        }

        public async Task<Reservation?> UpdateReservation(Reservation reservation)
        {
            try
            {
                if (reservation == null || string.IsNullOrWhiteSpace(reservation.Id))
                    return null;

                await _databaseClient
                    .Child("reservations")
                    .Child(reservation.Id)
                    .PutAsync(reservation);

                return reservation;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating reservation: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> AddMenuItemsToReservation(string reservationId, List<MenuSelection> items)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reservationId) || items == null || items.Count == 0)
                    return false;

                var reservation = await _databaseClient
                    .Child("reservations")
                    .Child(reservationId)
                    .OnceSingleAsync<Reservation>();

                if (reservation == null)
                    return false;

                // Merge into reservation.MenuItems in-memory
                var existing = reservation.MenuItems ?? new Dictionary<string, int>();

                foreach (var it in items)
                {
                    if (existing.ContainsKey(it.MenuItemId))
                        existing[it.MenuItemId] += it.Quantity;
                    else
                        existing[it.MenuItemId] = it.Quantity;
                }

                reservation.MenuItems = existing;

                await _databaseClient.Child($"reservations/{reservationId}").PutAsync(reservation);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding menu items: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddCustomHookahToReservation(string reservationId, HookahCustomMix mix)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reservationId) || mix == null || string.IsNullOrWhiteSpace(mix.Id))
                    return false;

                var reservation = await _databaseClient
                    .Child("reservations")
                    .Child(reservationId)
                    .OnceSingleAsync<Reservation>();

                if (reservation == null)
                    return false;

                reservation.MenuItems ??= new Dictionary<string, int>();
                reservation.HookahMixes ??= new Dictionary<string, HookahCustomMix>();

                reservation.MenuItems[mix.Id] = 1;
                reservation.HookahMixes[mix.Id] = mix;

                await _databaseClient.Child($"reservations/{reservationId}").PutAsync(reservation);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding custom hookah: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CancelReservation(string reservationId, string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reservationId))
                    return false;

                var reservation = await _databaseClient
                    .Child("reservations")
                    .Child(reservationId)
                    .OnceSingleAsync<Reservation>();

                if (reservation == null)
                    return false;

                if (reservation.UserId != userId)
                    return false;

                if (reservation.UserId != _firebaseAuthService.CurrentUserId)
                    return false;

                reservation.Status = ReservationStatus.Cancelled;

                await _databaseClient
                    .Child("reservations")
                    .Child(reservationId)
                    .PutAsync(reservation);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cancelling reservation: {ex.Message}");
                return false;
            }
        }

        private async Task<List<Reservation>> FetchAllReservationsFromDatabaseAsync()
        {
            var reservations = await _databaseClient
                .Child("reservations")
                .OnceAsync<Reservation>();

            return reservations
                .Where(r => r.Object != null)
                .Select(r => MapFirebaseReservation(r.Key, r.Object))
                .OrderByDescending(r => r.ReservationDate)
                .ThenBy(r => r.StartTime)
                .ToList();
        }

        private static bool IsHistorical(Reservation reservation)
        {
            if (reservation.Status == ReservationStatus.Cancelled)
                return true;

            var reservationEnd = reservation.ReservationDate.Date.Add(reservation.EndTime);
            return reservationEnd < DateTime.Now;
        }

        private Reservation MapFirebaseReservation(string key, Reservation raw)
        {
            return new Reservation
            {
                Id = key,
                UserId = raw.UserId,
                UserName = raw.UserName ?? string.Empty,
                TableNumber = raw.TableNumber,
                ReservationDate = raw.ReservationDate,
                StartTime = raw.StartTime,
                EndTime = raw.EndTime,
                NumberOfGuests = raw.NumberOfGuests,
                SpecialRequests = raw.SpecialRequests ?? string.Empty,
                Status = raw.Status,
                CreatedAt = raw.CreatedAt,
                ExpiresAt = raw.ExpiresAt,
                MenuItems = raw.MenuItems ?? new System.Collections.Generic.Dictionary<string,int>(),
                HookahMixes = raw.HookahMixes ?? new System.Collections.Generic.Dictionary<string, HookahCustomMix>()
            };
        }

        private async Task CancelExpiredReservations(List<Reservation> reservations)
        {
            var expired = reservations.Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt.HasValue && r.ExpiresAt.Value.ToUniversalTime() < DateTime.UtcNow).ToList();
            foreach (var ex in expired)
            {
                ex.Status = ReservationStatus.Cancelled;
                try
                {
                    await _databaseClient
                        .Child("reservations")
                        .Child(ex.Id)
                        .PutAsync(ex);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error marking expired reservation as cancelled: {e.Message}");
                }
            }
        }
    }
}
