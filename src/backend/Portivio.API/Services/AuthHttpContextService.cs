using Portivio.Application.DTOs.Auth;

namespace Portivio.API.Services
{
    public interface IAuthHttpContextService
    {
        LoginRequest CreateLoginRequest(HttpContext httpContext, LoginRequest request);
        GoogleLoginRequest CreateGoogleLoginRequest(HttpContext httpContext, GoogleLoginRequest request);
        AuthResponse? CreateClientAuthResponse(HttpContext httpContext, AuthResponse? response);
        void ApplyRefreshTokenCookie(HttpResponse response, AuthResponse? authResponse);
        void ClearAuthCookies(HttpResponse response);
    }

    public class AuthHttpContextService : IAuthHttpContextService
    {
        public LoginRequest CreateLoginRequest(HttpContext httpContext, LoginRequest request)
        {
            return new LoginRequest
            {
                Email = request.Email,
                Password = request.Password,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                DeviceInfo = GetUserAgent(httpContext),
                IssueRefreshToken = true
            };
        }

        public GoogleLoginRequest CreateGoogleLoginRequest(HttpContext httpContext, GoogleLoginRequest request)
        {
            return new GoogleLoginRequest
            {
                Token = request.Token,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                DeviceInfo = GetUserAgent(httpContext)
            };
        }

        public AuthResponse? CreateClientAuthResponse(HttpContext httpContext, AuthResponse? response)
        {
            if (response == null)
            {
                return null;
            }

            var exposeRefreshToken = IsPhoneClient(GetUserAgent(httpContext));
            return new AuthResponse
            {
                Success = response.Success,
                Message = response.Message,
                AccessToken = response.AccessToken,
                RefreshToken = exposeRefreshToken ? response.RefreshToken : null,
                User = response.User,
                AccessTokenExpiry = response.AccessTokenExpiry,
                RefreshTokenExpiry = exposeRefreshToken ? response.RefreshTokenExpiry : null
            };
        }

        public void ApplyRefreshTokenCookie(HttpResponse response, AuthResponse? authResponse)
        {
            if (!string.IsNullOrWhiteSpace(authResponse?.RefreshToken) && authResponse.RefreshTokenExpiry.HasValue)
            {
                response.Cookies.Append("refreshToken", authResponse.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = authResponse.RefreshTokenExpiry
                });
                return;
            }

            response.Cookies.Delete("refreshToken");
        }

        public void ClearAuthCookies(HttpResponse response)
        {
            response.Cookies.Delete("refreshToken");
            response.Cookies.Delete("accessToken");
            response.Cookies.Delete("sessionToken");
        }

        private static string GetUserAgent(HttpContext httpContext)
        {
            return httpContext.Request.Headers.UserAgent.ToString();
        }

        private static bool IsPhoneClient(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return false;
            }

            var normalized = userAgent.ToLowerInvariant();
            return normalized.Contains("iphone")
                || normalized.Contains("android")
                || normalized.Contains("mobile")
                || normalized.Contains("windows phone");
        }
    }
}
