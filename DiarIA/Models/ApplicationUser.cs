using Microsoft.AspNetCore.Identity;

namespace DiarIA.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsPremium { get; set; } = false;
    }
}