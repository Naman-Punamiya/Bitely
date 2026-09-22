using System.Collections.Generic;

namespace Bitely.Models
{
    public class FoodStall
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public bool IsOpen { get; set; }

        public User? Owner { get; set; }
        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}
