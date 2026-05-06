using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Portivio.Application.DTOs.Auth
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = null!;

        [Required]
        [StringLength(128, MinimumLength = 1)]
        public string Password { get; set; } = null!;

        [JsonIgnore]
        public string? DeviceInfo { get; set; }
        [JsonIgnore]
        public string? IpAddress { get; set; }
        [JsonIgnore]
        public bool IssueRefreshToken { get; set; }
    }
}
