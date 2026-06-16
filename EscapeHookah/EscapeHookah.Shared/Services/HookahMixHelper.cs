using System;
using System.Collections.Generic;
using System.Linq;
using EscapeHookah.Shared.Models;

namespace EscapeHookah.Shared.Services
{
    public static class HookahMixHelper
    {
        public const string PricingAdalya = "Adalya Hookah";
        public const string PricingBlonde = "Blonde Hookah";
        public const string PricingDark = "Dark Hookah";
        public const string PricingMix = "Mix Hookah";

        public const string DescAdalya = "Adalya";
        public const string DescBlonde = "Blonde";
        public const string DescDark = "Dark";

        private static readonly HashSet<string> PricingNames = new(StringComparer.OrdinalIgnoreCase)
        {
            PricingAdalya, PricingBlonde, PricingDark, PricingMix
        };

        public static bool IsHookahPricingItem(MenuItem item) =>
            PricingNames.Contains(item.Name);

        public static bool IsHookahFlavorItem(MenuItem item) =>
            string.Equals(item.Category, "Hookah", StringComparison.OrdinalIgnoreCase)
            && !IsHookahPricingItem(item)
            && !string.IsNullOrWhiteSpace(item.Description);

        public static HookahMixType? GetMixTypeFromPricingItem(MenuItem item)
        {
            if (string.Equals(item.Name, PricingAdalya, StringComparison.OrdinalIgnoreCase))
                return HookahMixType.Adalya;
            if (string.Equals(item.Name, PricingBlonde, StringComparison.OrdinalIgnoreCase))
                return HookahMixType.Blonde;
            if (string.Equals(item.Name, PricingDark, StringComparison.OrdinalIgnoreCase))
                return HookahMixType.Dark;
            if (string.Equals(item.Name, PricingMix, StringComparison.OrdinalIgnoreCase))
                return HookahMixType.Mix;
            return null;
        }

        public static MenuItem? FindPricingItem(IEnumerable<MenuItem> menu, HookahMixType mixType)
        {
            var targetName = mixType switch
            {
                HookahMixType.Adalya => PricingAdalya,
                HookahMixType.Blonde => PricingBlonde,
                HookahMixType.Dark => PricingDark,
                HookahMixType.Mix => PricingMix,
                _ => string.Empty
            };

            return menu.FirstOrDefault(m => string.Equals(m.Name, targetName, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<MenuItem> GetFlavorsForType(IEnumerable<MenuItem> menu, HookahMixType mixType)
        {
            var flavors = menu.Where(IsHookahFlavorItem);

            return mixType switch
            {
                HookahMixType.Adalya => flavors.Where(f => string.Equals(f.Description, DescAdalya, StringComparison.OrdinalIgnoreCase)),
                HookahMixType.Blonde => flavors.Where(f => string.Equals(f.Description, DescBlonde, StringComparison.OrdinalIgnoreCase)),
                HookahMixType.Dark => flavors.Where(f => string.Equals(f.Description, DescDark, StringComparison.OrdinalIgnoreCase)),
                HookahMixType.Mix => flavors.Where(f =>
                    string.Equals(f.Description, DescBlonde, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(f.Description, DescDark, StringComparison.OrdinalIgnoreCase)),
                _ => Enumerable.Empty<MenuItem>()
            };
        }

        public static bool ValidateMix(
            HookahMixType mixType,
            Dictionary<string, int> percentages,
            IEnumerable<MenuItem> allMenuItems,
            out string error)
        {
            error = string.Empty;
            var flavorLookup = allMenuItems.Where(IsHookahFlavorItem).ToDictionary(f => f.Id);
            var active = percentages.Where(p => p.Value > 0).ToList();

            if (active.Count == 0)
            {
                error = "Select at least one flavor.";
                return false;
            }

            var total = active.Sum(x => x.Value);
            if (total != 100)
            {
                error = $"Flavor percentages must total 100% (currently {total}%).";
                return false;
            }

            foreach (var (id, pct) in active)
            {
                if (pct % 5 != 0)
                {
                    error = "Each flavor must use 5% steps.";
                    return false;
                }

                if (!flavorLookup.ContainsKey(id))
                {
                    error = "One or more selected flavors are invalid.";
                    return false;
                }
            }

            if (mixType == HookahMixType.Mix)
            {
                var blondeSum = active
                    .Where(x => string.Equals(flavorLookup[x.Key].Description, DescBlonde, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Value);
                var darkSum = active
                    .Where(x => string.Equals(flavorLookup[x.Key].Description, DescDark, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Value);

                if (blondeSum != 50 || darkSum != 50)
                {
                    error = "Blonde + Dark mix must be exactly 50% Blonde and 50% Dark.";
                    return false;
                }

                foreach (var (id, _) in active)
                {
                    var desc = flavorLookup[id].Description;
                    if (!string.Equals(desc, DescBlonde, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(desc, DescDark, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "Mix can only include Blonde and Dark flavors.";
                        return false;
                    }
                }
            }
            else
            {
                var expected = mixType switch
                {
                    HookahMixType.Adalya => DescAdalya,
                    HookahMixType.Blonde => DescBlonde,
                    HookahMixType.Dark => DescDark,
                    _ => string.Empty
                };

                foreach (var (id, _) in active)
                {
                    if (!string.Equals(flavorLookup[id].Description, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"Only {expected} flavors can be mixed in this hookah.";
                        return false;
                    }
                }
            }

            return true;
        }

        public static string BuildDisplayName(Dictionary<string, int> percentages, IEnumerable<MenuItem> allMenuItems)
        {
            var lookup = allMenuItems.ToDictionary(m => m.Id);
            var parts = percentages
                .Where(p => p.Value > 0 && lookup.ContainsKey(p.Key))
                .OrderByDescending(p => p.Value)
                .Select(p => $"{lookup[p.Key].Name} {p.Value}%");

            return "Custom Hookah: " + string.Join(", ", parts);
        }

        public static decimal GetLinePrice(string menuItemKey, int quantity, Reservation reservation, Dictionary<string, MenuItem> menuLookup)
        {
            if (reservation.HookahMixes != null
                && reservation.HookahMixes.TryGetValue(menuItemKey, out var mix)
                && menuLookup.TryGetValue(mix.PricingMenuItemId, out var pricing))
            {
                return pricing.Price * quantity;
            }

            if (menuLookup.TryGetValue(menuItemKey, out var item))
                return item.Price * quantity;

            return 0m;
        }

        public static string GetLineDisplayName(string menuItemKey, int quantity, Reservation reservation, Dictionary<string, MenuItem> menuLookup)
        {
            if (reservation.HookahMixes != null && reservation.HookahMixes.TryGetValue(menuItemKey, out var mix))
                return mix.DisplayName + (quantity > 1 ? $" x{quantity}" : string.Empty);

            if (menuLookup.TryGetValue(menuItemKey, out var item))
                return item.Name + (quantity > 1 ? $" x{quantity}" : string.Empty);

            return menuItemKey;
        }
    }
}
