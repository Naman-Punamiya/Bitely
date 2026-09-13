using System;
using System.Collections.Generic;

namespace Bitely.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int ConsumerId { get; set; }
        public User? Consumer { get; set; }
        public int FoodStallId { get; set; }
        public FoodStall? FoodStall { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public Payment? Payment { get; set; }
    }
}
