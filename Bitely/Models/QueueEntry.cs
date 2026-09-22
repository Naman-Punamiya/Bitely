namespace Bitely.Models
{
    public class QueueEntry
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }
        public int FoodStallId { get; set; }
        public FoodStall? FoodStall { get; set; }
        public int QueueNumber { get; set; }
        public string Status { get; set; } = "Pending";
    }
}
