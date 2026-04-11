using System.Text.Json.Serialization;

namespace Portivio.Application.DTOs.Auth
{
    public class LoginRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        [JsonIgnore]
        public string? DeviceInfo { get; set; }
        [JsonIgnore]
        public string? IpAddress { get; set; }
        [JsonIgnore]
        public bool IssueRefreshToken { get; set; }
    }
}
