using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EscapeHookah.Shared.Services;
using System;
using System.Linq;
using System.Collections.Generic;
using EscapeHookah.Shared.Models;
using Firebase.Database;
using Newtonsoft.Json;

namespace EscapeHookah.Web.Services
{
    public class ReservationNotifierHostedService : BackgroundService
    {
        private readonly ILogger<ReservationNotifierHostedService> _logger;
        private readonly IFirebaseAuthService _authService;
        private readonly FcmService _fcmService;
        private readonly FirebaseClient _dbClient;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

        public ReservationNotifierHostedService(ILogger<ReservationNotifierHostedService> logger, IFirebaseAuthService authService, FcmService fcmService)
        {
            _logger = logger;
            _authService = authService;
            _fcmService = fcmService;
            _dbClient = new FirebaseClient("https://escapehookah-781e5-default-rtdb.europe-west1.firebasedatabase.app/");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForHoldExpirations();
                    await CheckForPreReservationNotifications();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ReservationNotifierHostedService loop");
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        private async Task CheckForHoldExpirations()
        {
            var reservations = await _dbClient.Child("reservations").OnceAsync<Reservation>();
            var now = DateTime.UtcNow;

            foreach (var r in reservations)
            {
                if (r.Object == null) continue;
                var res = r.Object;
                if (res.Status == ReservationStatus.Pending && res.ExpiresAt.HasValue && res.ExpiresAt.Value.ToUniversalTime() <= now)
                {
                    // Mark cancelled and notify owner
                    res.Status = ReservationStatus.Cancelled;
                    await _dbClient.Child($"reservations/{r.Key}").PutAsync(JsonConvert.SerializeObject(res));

                    // get device tokens for user
                    var tokens = await _dbClient.Child($"deviceTokens/{res.UserId}").OnceAsync<string>();
                    foreach (var t in tokens)
                    {
                        try
                        {
                            await _fcmService.SendNotificationAsync(t.Object, "Hold expired", $"Your hold for Table {res.TableNumber} at {res.StartTime:hh\\:mm} has expired.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed sending hold expired notification");
                        }
                    }
                }
            }
        }

        private async Task CheckForPreReservationNotifications()
        {
            var reservations = await _dbClient.Child("reservations").OnceAsync<Reservation>();
            var now = DateTime.UtcNow;

            foreach (var r in reservations)
            {
                if (r.Object == null) continue;
                var res = r.Object;
                if (res.Status == ReservationStatus.Confirmed)
                {
                    var reservationDateTime = res.ReservationDate.Date + res.StartTime;
                    var notifyAt = reservationDateTime - TimeSpan.FromMinutes(15);
                    if (notifyAt.ToUniversalTime() <= now && reservationDateTime.ToUniversalTime() > now)
                    {
                        // send notification and set a marker so we don't send twice
                        var markerKey = $"notifications_sent/{r.Key}";
                        var marker = await _dbClient.Child(markerKey).OnceSingleAsync<bool?>();
                        if (marker == null || marker == false)
                        {
                            var tokens = await _dbClient.Child($"deviceTokens/{res.UserId}").OnceAsync<string>();
                            foreach (var t in tokens)
                            {
                                try
                                {
                                    await _fcmService.SendNotificationAsync(t.Object, "Reservation reminder", $"Reminder: your reservation for Table {res.TableNumber} at {res.StartTime:hh\\:mm} starts in 15 minutes.");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed sending pre-reservation notification");
                                }
                            }

                            await _dbClient.Child($"notifications_sent/{r.Key}").PutAsync(true.ToString());
                        }
                    }
                }
            }
        }
    }
}
