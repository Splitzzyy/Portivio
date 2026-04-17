using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portivio.Application.DTOs.Instrument;
using Portivio.Application.Results;
using Portivio.Application.Services;

namespace Portivio.API.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("per-user")]
    [Route("api/asset-types")]
    public class AssetTypeController : ControllerBase
    {
        private readonly IInstrumentService _instrumentService;
        private readonly ILogger<AssetTypeController> _logger;

        public AssetTypeController(IInstrumentService instrumentService, ILogger<AssetTypeController> logger)
        {
            _instrumentService = instrumentService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAssetTypes()
        {
            try
            {
                var result = await _instrumentService.GetAssetTypesAsync();
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching asset types");
                return StatusCode(500, new { success = false, message = "An error occurred while fetching asset types" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateAssetType([FromBody] CreateAssetTypeRequest request)
        {
            try
            {
                var result = await _instrumentService.CreateAssetTypeAsync(request);
                return result.Match(
                    onSuccess: () => StatusCode(201, result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating asset type");
                return StatusCode(500, new { success = false, message = "An error occurred while creating the asset type" });
            }
        }

        [HttpDelete("{assetTypeId:guid}")]
        public async Task<IActionResult> DeleteAssetType(Guid assetTypeId)
        {
            try
            {
                var result = await _instrumentService.DeleteAssetTypeAsync(assetTypeId);
                return result.Match<IActionResult>(
                    onSuccess: () => NoContent(),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting asset type {AssetTypeId}", assetTypeId);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the asset type" });
            }
        }
    }
}
