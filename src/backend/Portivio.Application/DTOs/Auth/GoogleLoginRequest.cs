namespace Portivio.Application.DTOs.Auth
{
    public class GoogleLoginRequest
    {
        public string Token { get; set; } = null!;
        public string? DeviceInfo { get; set; }
        public string? IpAddress { get; set; }
    }
}
