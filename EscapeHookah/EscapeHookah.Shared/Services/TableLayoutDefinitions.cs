using EscapeHookah.Shared.Models;

namespace EscapeHookah.Shared.Services
{
    public static class TableLayoutDefinitions
    {
        public static IReadOnlyList<TableAreaLayout> Areas { get; } = new List<TableAreaLayout>
        {
            new()
            {
                Name = "Closed Terrace",
                LayoutClass = "terrace-2x2",
                Tables = new List<Table>
                {
                    new() { TableNumber = 1, Capacity = 10, Area = "Closed Terrace", Location = "Top left", LayoutSlot = "terrace-tl", Highlight = "Projector view" },
                    new() { TableNumber = 2, Capacity = 10, Area = "Closed Terrace", Location = "Top right", LayoutSlot = "terrace-tr" },
                    new() { TableNumber = 3, Capacity = 10, Area = "Closed Terrace", Location = "Bottom left", LayoutSlot = "terrace-bl" },
                    new() { TableNumber = 4, Capacity = 10, Area = "Closed Terrace", Location = "Bottom right", LayoutSlot = "terrace-br" }
                }
            },
            new()
            {
                Name = "Inside Basement",
                LayoutClass = "basement-l",
                Tables = new List<Table>
                {
                    new() { TableNumber = 5, Capacity = 5, Area = "Inside Basement", Location = "Upper left", LayoutSlot = "basement-l-top-left" },
                    new() { TableNumber = 6, Capacity = 5, Area = "Inside Basement", Location = "Upper right", LayoutSlot = "basement-l-top-right" },
                    new() { TableNumber = 7, Capacity = 2, Area = "Inside Basement", Location = "Corner", LayoutSlot = "basement-l-corner", Highlight = "Corner · 2 seats" }
                }
            },
            new()
            {
                Name = "Billiard Room",
                LayoutClass = "billiard-single",
                Tables = new List<Table>
                {
                    new() { TableNumber = 8, Capacity = 8, Area = "Billiard Room", Location = "Center", LayoutSlot = "billiard-center" }
                }
            },
            new()
            {
                Name = "Outside",
                LayoutClass = "outside-row",
                Tables = new List<Table>
                {
                    new() { TableNumber = 9, Capacity = 6, Area = "Outside", Location = "Table 1", LayoutSlot = "outside-1" },
                    new() { TableNumber = 10, Capacity = 6, Area = "Outside", Location = "Table 2", LayoutSlot = "outside-2" },
                    new() { TableNumber = 11, Capacity = 6, Area = "Outside", Location = "Table 3", LayoutSlot = "outside-3" },
                    new() { TableNumber = 12, Capacity = 6, Area = "Outside", Location = "Table 4", LayoutSlot = "outside-4" }
                }
            }
        };

        public static List<Table> AllTables => Areas.SelectMany(a => a.Tables).ToList();

        public static Table? FindTable(int tableNumber) =>
            AllTables.FirstOrDefault(t => t.TableNumber == tableNumber);
    }
}
