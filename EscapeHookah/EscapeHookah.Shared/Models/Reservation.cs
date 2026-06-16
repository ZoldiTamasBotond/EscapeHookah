using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EscapeHookah.Shared.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ReservationStatus
    {
        Pending = 0,
        Confirmed = 1,
        Cancelled = 2
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
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public System.Collections.Generic.Dictionary<string,int> MenuItems { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, HookahCustomMix> HookahMixes { get; set; } = new();
    }
}
