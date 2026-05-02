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

        public AssetTypeController(IInstrumentService instrumentService)
        {
            _instrumentService = instrumentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAssetTypes()
        {
            var result = await _instrumentService.GetAssetTypesAsync();
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost]
        public async Task<IActionResult> CreateAssetType([FromBody] CreateAssetTypeRequest request)
        {
            var result = await _instrumentService.CreateAssetTypeAsync(request);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpDelete("{assetTypeId:guid}")]
        public async Task<IActionResult> DeleteAssetType(Guid assetTypeId)
        {
            var result = await _instrumentService.DeleteAssetTypeAsync(assetTypeId);
            return result.Match<IActionResult>(
                onSuccess: () => NoContent(),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
