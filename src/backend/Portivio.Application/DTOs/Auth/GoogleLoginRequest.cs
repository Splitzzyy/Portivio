using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Portivio.Application.DTOs.Auth
{
    public class GoogleLoginRequest
    {
        [Required]
        [StringLength(4096, MinimumLength = 1)]
        public string Token { get; set; } = null!;

        [JsonIgnore]
        public string? DeviceInfo { get; set; }
        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}
