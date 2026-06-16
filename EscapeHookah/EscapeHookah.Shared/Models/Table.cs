namespace EscapeHookah.Shared.Models
{
    public class Table
    {
        public int TableNumber { get; set; }
        public int Capacity { get; set; }
        public string Area { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? Highlight { get; set; }
        public string LayoutSlot { get; set; } = string.Empty;
    }

    public class TableAreaLayout
    {
        public string Name { get; set; } = string.Empty;
        public string LayoutClass { get; set; } = string.Empty;
        public List<Table> Tables { get; set; } = new();
    }
}
