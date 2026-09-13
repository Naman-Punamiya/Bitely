namespace Bitely.Models
{
    public class MenuItem
    {
        public int Id { get; set; }
        public int FoodStallId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }

        public FoodStall? FoodStall { get; set; }
    }
}
