using System;

namespace EscapeHookah.Shared.Models
{
    public enum ReservationStatus
    {
        Confirmed = 0,
        Cancelled = 1
    }

    public class Reservation
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int TableNumber { get; set; }
        public DateTime ReservationDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string SpecialRequests { get; set; } = string.Empty;
        public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}