using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.Home;
using Portivio.Application.Results;
using Portivio.Application.Services;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/[controller]")]
    public class HomeController : PortivioControllerBase
    {
        private readonly IHomeService _homeService;

        public HomeController(IHomeService homeService)
        {
            _homeService = homeService;
        }

        /// <summary>
        /// Get all data for the authenticated user: profile, portfolio, holdings, transactions, SIPs, latest performance.
        /// Read-only. User id resolved from JWT claim.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();
            var result = await _homeService.GetHomeDataAsync(userId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
