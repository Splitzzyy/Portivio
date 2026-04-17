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
        private readonly ILogger<TransactionController> _logger;

        public TransactionController(ITransactionService transactionService, ILogger<TransactionController> logger)
        {
            _transactionService = transactionService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactions(Guid profileId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transactions for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching transactions" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransaction(Guid profileId, [FromBody] CreateTransactionRequest request)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction for profile {ProfileId}", profileId);
                return StatusCode(500, new { success = false, message = "An error occurred while creating the transaction" });
            }
        }

        [HttpPut("{txId:guid}")]
        public async Task<IActionResult> UpdateTransaction(Guid profileId, Guid txId, [FromBody] UpdateTransactionRequest request)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction {TxId}", txId);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the transaction" });
            }
        }

        [HttpDelete("{txId:guid}")]
        public async Task<IActionResult> DeleteTransaction(Guid profileId, Guid txId)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction {TxId}", txId);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the transaction" });
            }
        }
    }
}
