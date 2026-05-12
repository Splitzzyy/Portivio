using System.ComponentModel.DataAnnotations;

namespace Portivio.Application.DTOs.Auth
{
    public class UpdateUserProfileRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = null!;
    }
}
