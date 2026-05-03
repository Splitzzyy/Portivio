using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.Asset;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/assets")]
    public class AssetController : ControllerBase
    {
        private readonly IAssetInstrumentService _assets;

        public AssetController(IAssetInstrumentService assets)
        {
            _assets = assets;
        }

        [HttpPost("mutual-fund")]
        public async Task<IActionResult> AddMutualFund(Guid profileId, [FromBody] AddMutualFundRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _assets.AddMutualFundAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("fixed-deposit")]
        public async Task<IActionResult> AddFixedDeposit(Guid profileId, [FromBody] AddFixedDepositRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _assets.AddFixedDepositAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("recurring-deposit")]
        public async Task<IActionResult> AddRecurringDeposit(Guid profileId, [FromBody] AddRecurringDepositRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _assets.AddRecurringDepositAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("ppf")]
        public async Task<IActionResult> AddPpf(Guid profileId, [FromBody] AddPpfRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _assets.AddPpfAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("ppf/contributions")]
        public async Task<IActionResult> AddPpfContribution(Guid profileId, [FromBody] AddPpfContributionRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _assets.AddPpfContributionAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("gold")]
        public async Task<IActionResult> AddGold(Guid profileId, [FromBody] AddGoldRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _assets.AddGoldAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("stock")]
        public async Task<IActionResult> AddStock(Guid profileId, [FromBody] AddStockRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _assets.AddStockAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        private bool TryGetUserId(out Guid userId)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim?.Value, out userId);
        }
    }
}
