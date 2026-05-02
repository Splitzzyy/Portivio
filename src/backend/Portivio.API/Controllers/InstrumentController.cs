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

        public InstrumentController(IInstrumentService instrumentService)
        {
            _instrumentService = instrumentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetInstruments([FromQuery] Guid? assetTypeId = null)
        {
            var result = await _instrumentService.GetInstrumentsAsync(assetTypeId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpGet("{instrumentId:guid}")]
        public async Task<IActionResult> GetInstrument(Guid instrumentId)
        {
            var result = await _instrumentService.GetInstrumentAsync(instrumentId);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPost]
        public async Task<IActionResult> CreateInstrument([FromBody] CreateInstrumentRequest request)
        {
            var result = await _instrumentService.CreateInstrumentAsync(request);
            return result.Match(
                onSuccess: () => StatusCode(201, result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpPut("{instrumentId:guid}")]
        public async Task<IActionResult> UpdateInstrument(Guid instrumentId, [FromBody] UpdateInstrumentRequest request)
        {
            var result = await _instrumentService.UpdateInstrumentAsync(instrumentId, request);
            return result.Match(
                onSuccess: () => Ok(result.Data),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }

        [HttpDelete("{instrumentId:guid}")]
        public async Task<IActionResult> DeleteInstrument(Guid instrumentId)
        {
            var result = await _instrumentService.DeleteInstrumentAsync(instrumentId);
            return result.Match<IActionResult>(
                onSuccess: () => NoContent(),
                onFailure: (error) => StatusCode(error.StatusCode ?? 400, new { success = false, message = error.Message, errors = error.Errors })
            );
        }
    }
}
