namespace Bitely.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        public int CartId { get; set; }
        public Cart? Cart { get; set; }

        public int MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public string? SpecialInstructions { get; set; }
    }
}
