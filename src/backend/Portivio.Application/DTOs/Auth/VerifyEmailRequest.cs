namespace Portivio.Application.DTOs.Auth
{
    public class VerifyEmailRequest
    {
        public string Email { get; set; } = null!;
        public string VerificationToken { get; set; } = null!;
    }
}
