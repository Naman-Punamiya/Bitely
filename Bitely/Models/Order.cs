using System;
using System.Collections.Generic;

namespace Bitely.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;

        public string CustomerId { get; set; } = string.Empty;
        public ApplicationUser? Customer { get; set; }

        public int FoodStallId { get; set; }
        public FoodStall? FoodStall { get; set; }

        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending";
        public string PaymentMethod { get; set; } = "Cash"; // "Cash" or "Online"
        public string? PaymentId { get; set; } // Razorpay Payment Id (pay_xxx)
        public string PaymentStatus { get; set; } = "Pending"; // "Pending" or "Paid"
        public string? CustomerNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
