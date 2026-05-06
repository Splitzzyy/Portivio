using System.ComponentModel.DataAnnotations;

namespace Portivio.Application.DTOs.Auth
{
    public class VerifyEmailRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(512, MinimumLength = 1)]
        public string VerificationToken { get; set; } = null!;
    }
}
