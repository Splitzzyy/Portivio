using System.ComponentModel.DataAnnotations;

namespace Portivio.Application.DTOs.Auth
{
    public class ChangePasswordRequest
    {
        [Required]
        [StringLength(128, MinimumLength = 6)]
        public string NewPassword { get; set; } = null!;

        [Required]
        public string ConfirmPassword { get; set; } = null!;
    }
}
