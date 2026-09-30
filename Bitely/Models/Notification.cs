using System;

namespace Bitely.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        public int? FoodStallId { get; set; }
        public FoodStall? FoodStall { get; set; }
    }
}
