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

        public ReservationService(IFirebaseAuthService firebaseAuthService)
        {
            _firebaseAuthService = firebaseAuthService;

            _databaseClient = new FirebaseClient(
                "https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/",
                new FirebaseOptions
                {
                    AuthTokenAsyncFactory = async () => await _firebaseAuthService.GetIdTokenAsync()
                });

            _tables = new List<Table>
            {
                new Table { TableNumber = 1, Capacity = 4, Location = "Main Hall - Window" },
                new Table { TableNumber = 2, Capacity = 6, Location = "Main Hall - Center" },
                new Table { TableNumber = 3, Capacity = 4, Location = "VIP Section" },
                new Table { TableNumber = 4, Capacity = 8, Location = "Garden Terrace" }
            };
        }

        public Task<List<Table>> GetTables()
        {
            return Task.FromResult(_tables);
        }

        public async Task<List<Reservation>> GetUserReservations(string userId)
        {
            try
            {
                var reservations = await _databaseClient
                    .Child("reservations")
                    .OnceAsync<Reservation>();

                return reservations
                    .Where(r => r.Object != null && r.Object.UserId == userId)
                    .Select(r => new Reservation
                    {
                        Id = r.Key,
                        UserId = r.Object.UserId,
                        UserName = r.Object.UserName ?? "",
                        TableNumber = r.Object.TableNumber,
                        ReservationDate = r.Object.ReservationDate,
                        StartTime = r.Object.StartTime,
                        EndTime = r.Object.EndTime,
                        NumberOfGuests = r.Object.NumberOfGuests,
                        SpecialRequests = r.Object.SpecialRequests ?? "",
                        Status = r.Object.Status,
                        CreatedAt = r.Object.CreatedAt
                    })
                    .OrderByDescending(r => r.ReservationDate)
                    .ThenBy(r => r.StartTime)
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user reservations: {ex.Message}");
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

                return reservations
                    .Where(r => r.Object != null)
                    .Select(r => new Reservation
                    {
                        Id = r.Key,
                        UserId = r.Object.UserId,
                        UserName = r.Object.UserName ?? "",
                        TableNumber = r.Object.TableNumber,
                        ReservationDate = r.Object.ReservationDate,
                        StartTime = r.Object.StartTime,
                        EndTime = r.Object.EndTime,
                        NumberOfGuests = r.Object.NumberOfGuests,
                        SpecialRequests = r.Object.SpecialRequests ?? "",
                        Status = r.Object.Status,
                        CreatedAt = r.Object.CreatedAt
                    })
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
    }
}