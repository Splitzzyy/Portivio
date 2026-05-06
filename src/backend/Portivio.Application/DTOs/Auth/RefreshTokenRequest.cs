using System.ComponentModel.DataAnnotations;

namespace Portivio.Application.DTOs.Auth
{
    public class RefreshTokenRequest
    {
        [StringLength(512)]
        public string? RefreshToken { get; set; }
    }
}
