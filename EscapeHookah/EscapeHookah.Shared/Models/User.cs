using System;
using System.Collections.Generic;

namespace EscapeHookah.Shared.Models
{
    public class User
    {
        public string? UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? UserName { get; set; }
        public string? EMail { get; set; }
        public string? PhoneNumber { get; set; }
        public long? DateOfBirth { get; set; } // Unix timestamp in milliseconds
        public string? Gender { get; set; }
        public string? Role { get; set; } = "User";
        public int? Rate { get; set; } = 0;
        public List<string>? ReservationsID { get; set; } = new List<string>();
        public long? CreatedAt { get; set; }
    }
}