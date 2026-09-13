using System.Collections.Generic;

namespace Bitely.Models
{
    public class Cart
    {
        public int Id { get; set; }
        public int ConsumerId { get; set; }
        public User? Consumer { get; set; }
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}
