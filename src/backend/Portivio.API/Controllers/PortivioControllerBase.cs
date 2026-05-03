using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    public abstract class PortivioControllerBase : ControllerBase
    {
        protected bool TryGetCurrentUserId(out Guid userId)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim?.Value, out userId);
        }

        protected IActionResult UserNotAuthenticated() =>
            Unauthorized(new { success = false, message = "User not authenticated" });
    }
}
