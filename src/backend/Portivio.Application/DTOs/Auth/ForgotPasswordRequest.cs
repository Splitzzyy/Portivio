using System.ComponentModel.DataAnnotations;

namespace Portivio.Application.DTOs.Auth
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = null!;
    }
}
