using Microsoft.AspNetCore.Identity;

namespace Bitely.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? Name { get; set; }
    }
}
