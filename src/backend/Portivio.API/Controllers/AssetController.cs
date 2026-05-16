using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.Asset;
using Portivio.Application.Results;
using Portivio.Application.Services;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/assets")]
    public class AssetController : PortivioControllerBase
    {
        private readonly IAssetInstrumentService _assets;

        public AssetController(IAssetInstrumentService assets)
        {
            _assets = assets;
        }

        [HttpPost("mutual-fund")]
        public async Task<IActionResult> AddMutualFund(Guid profileId, [FromBody] AddMutualFundRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.AddMutualFundAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPut("mutual-fund/{instrumentId:guid}")]
        public async Task<IActionResult> UpdateMutualFund(Guid profileId, Guid instrumentId, [FromBody] UpdateMutualFundRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.UpdateMutualFundAsync(userId, profileId, instrumentId, req, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("fixed-deposit")]
        public async Task<IActionResult> AddFixedDeposit(Guid profileId, [FromBody] AddFixedDepositRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.AddFixedDepositAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPut("fixed-deposit/{instrumentId:guid}")]
        public async Task<IActionResult> UpdateFixedDeposit(Guid profileId, Guid instrumentId, [FromBody] UpdateFixedDepositRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.UpdateFixedDepositAsync(userId, profileId, instrumentId, req, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("recurring-deposit")]
        public async Task<IActionResult> AddRecurringDeposit(Guid profileId, [FromBody] AddRecurringDepositRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.AddRecurringDepositAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPut("recurring-deposit/{instrumentId:guid}")]
        public async Task<IActionResult> UpdateRecurringDeposit(Guid profileId, Guid instrumentId, [FromBody] UpdateRecurringDepositRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.UpdateRecurringDepositAsync(userId, profileId, instrumentId, req, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("ppf")]
        public async Task<IActionResult> AddPpf(Guid profileId, [FromBody] AddPpfRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.AddPpfAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPut("ppf/{instrumentId:guid}")]
        public async Task<IActionResult> UpdatePpf(Guid profileId, Guid instrumentId, [FromBody] UpdatePpfRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.UpdatePpfAsync(userId, profileId, instrumentId, req, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("ppf/contributions")]
        public async Task<IActionResult> AddPpfContribution(Guid profileId, [FromBody] AddPpfContributionRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.AddPpfContributionAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("gold")]
        public async Task<IActionResult> AddGold(Guid profileId, [FromBody] AddGoldRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.AddGoldAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPut("gold/{instrumentId:guid}")]
        public async Task<IActionResult> UpdateGold(Guid profileId, Guid instrumentId, [FromBody] UpdateGoldRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.UpdateGoldAsync(userId, profileId, instrumentId, req, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPost("stock")]
        public async Task<IActionResult> AddStock(Guid profileId, [FromBody] AddStockRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.AddStockAsync(userId, profileId, req, ct);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

        [HttpPut("stock/{instrumentId:guid}")]
        public async Task<IActionResult> UpdateStock(Guid profileId, Guid instrumentId, [FromBody] UpdateStockRequest req, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return UserNotAuthenticated();

            var result = await _assets.UpdateStockAsync(userId, profileId, instrumentId, req, ct);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: e => StatusCode(e.StatusCode ?? 400, new { success = false, message = e.Message, errors = e.Errors }));
        }

    }
}
