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
    [Route("api/instruments")]
    public class InstrumentController : ControllerBase
    {
        private readonly IInstrumentService _instrumentService;
        private readonly ILogger<InstrumentController> _logger;

        public InstrumentController(IInstrumentService instrumentService, ILogger<InstrumentController> logger)
        {
            _instrumentService = instrumentService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetInstruments([FromQuery] Guid? assetTypeId = null)
        {
            try
            {
                var result = await _instrumentService.GetInstrumentsAsync(assetTypeId);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching instruments");
                return StatusCode(500, new { success = false, message = "An error occurred while fetching instruments" });
            }
        }

        [HttpGet("{instrumentId:guid}")]
        public async Task<IActionResult> GetInstrument(Guid instrumentId)
        {
            try
            {
                var result = await _instrumentService.GetInstrumentAsync(instrumentId);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching instrument {InstrumentId}", instrumentId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching the instrument" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateInstrument([FromBody] CreateInstrumentRequest request)
        {
            try
            {
                var result = await _instrumentService.CreateInstrumentAsync(request);
                return result.Match(
                    onSuccess: () => StatusCode(201, result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating instrument");
                return StatusCode(500, new { success = false, message = "An error occurred while creating the instrument" });
            }
        }

        [HttpPut("{instrumentId:guid}")]
        public async Task<IActionResult> UpdateInstrument(Guid instrumentId, [FromBody] UpdateInstrumentRequest request)
        {
            try
            {
                var result = await _instrumentService.UpdateInstrumentAsync(instrumentId, request);
                return result.Match(
                    onSuccess: () => Ok(result.Data),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating instrument {InstrumentId}", instrumentId);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the instrument" });
            }
        }

        [HttpDelete("{instrumentId:guid}")]
        public async Task<IActionResult> DeleteInstrument(Guid instrumentId)
        {
            try
            {
                var result = await _instrumentService.DeleteInstrumentAsync(instrumentId);
                return result.Match<IActionResult>(
                    onSuccess: () => NoContent(),
                    onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting instrument {InstrumentId}", instrumentId);
                return StatusCode(500, new { success = false, message = "An error occurred while deleting the instrument" });
            }
        }
    }
}
