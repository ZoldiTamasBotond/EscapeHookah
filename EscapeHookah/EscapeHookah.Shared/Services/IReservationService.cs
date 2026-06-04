using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EscapeHookah.Shared.Models;

namespace EscapeHookah.Shared.Services
{
    public interface IReservationService
    {
        Task<List<Table>> GetTables();
        Task<List<Reservation>> GetUserReservations(string userId);
        Task<List<Reservation>> GetReservationsByDate(DateTime date);
        Task<bool> IsTableAvailable(int tableNumber, DateTime date, TimeSpan startTime, TimeSpan endTime);
        Task<Reservation?> CreateReservation(Reservation reservation);
        Task<bool> CancelReservation(string reservationId, string userId);
    }
}