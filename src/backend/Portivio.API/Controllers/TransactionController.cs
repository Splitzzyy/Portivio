using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.Transaction;
using Portivio.Application.Results;
using Portivio.Application.Services;
using System.Security.Claims;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/profiles/{profileId:guid}/transactions")]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactions(Guid profileId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _transactionService.GetTransactionsAsync(userId, profileId, page, pageSize);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransaction(Guid profileId, [FromBody] CreateTransactionRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _transactionService.CreateTransactionAsync(userId, profileId, request);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPut("{txId:guid}")]
        public async Task<IActionResult> UpdateTransaction(Guid profileId, Guid txId, [FromBody] UpdateTransactionRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _transactionService.UpdateTransactionAsync(userId, profileId, txId, request);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpDelete("{txId:guid}")]
        public async Task<IActionResult> DeleteTransaction(Guid profileId, Guid txId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var result = await _transactionService.DeleteTransactionAsync(userId, profileId, txId);
            return result.Match<IActionResult>(
                onSuccess: () => NoContent(),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
