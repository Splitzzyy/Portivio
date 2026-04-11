using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portivio.Application.DTOs.Home;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly IHomeService _homeService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IHomeService homeService, ILogger<HomeController> logger)
        {
            _homeService = homeService;
            _logger = logger;
        }

        /// <summary>
        /// Get all data for the authenticated user: profile, portfolio, holdings, transactions, SIPs, latest performance.
        /// Read-only. User id resolved from JWT claim.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<HomeResponse>> Get()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var result = await _homeService.GetHomeDataAsync(userId);

                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching home data");
                return StatusCode(500, new { success = false, message = "An error occurred while fetching home data" });
            }
        }
    }
}
