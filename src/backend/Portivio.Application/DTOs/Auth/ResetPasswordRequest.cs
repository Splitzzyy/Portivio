using System.ComponentModel.DataAnnotations;
using Portivio.Application.Validation;

namespace Portivio.Application.DTOs.Auth
{
    public class ResetPasswordRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(512, MinimumLength = 1)]
        public string ResetToken { get; set; } = null!;

        [Required]
        [StrongPassword]
        public string NewPassword { get; set; } = null!;

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
