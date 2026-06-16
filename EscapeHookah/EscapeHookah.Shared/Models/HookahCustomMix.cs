using System.Collections.Generic;

namespace EscapeHookah.Shared.Models
{
    public enum HookahMixType
    {
        Adalya,
        Blonde,
        Dark,
        Mix
    }

    public class HookahCustomMix
    {
        public string Id { get; set; } = string.Empty;
        public string PricingMenuItemId { get; set; } = string.Empty;
        public HookahMixType MixType { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public Dictionary<string, int> FlavorPercentages { get; set; } = new();
    }
}
