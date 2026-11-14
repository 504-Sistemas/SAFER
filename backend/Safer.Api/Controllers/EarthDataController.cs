using Microsoft.AspNetCore.Mvc;
using Safer.Api.Models;
using Safer.Api.Services.DecisionEngine;
using Safer.Api.Services.Nasa;
using Safer.Api.Services.Soil;

namespace Safer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EarthDataController(
    INasaPowerService nasaPowerService,
    INasaEarthdataService nasaEarthdataService,
    ISoilService soilService,
    IPestRiskService pestRiskService) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<ActionResult<object>> AnalyzeParcel([FromBody] ParsedParcelDto parcel, CancellationToken ct)
    {
        if (parcel.Centroid is null)
            return BadRequest("La parcela debe incluir su centroide y bounding box.");

        var climateTask = nasaPowerService.GetClimatologyAsync(parcel.Centroid.Latitude, parcel.Centroid.Longitude, ct);
        var soilTask = soilService.GetSoilProfileAsync(parcel.Centroid.Latitude, parcel.Centroid.Longitude, null, ct);

        await Task.WhenAll(climateTask, soilTask);

        var earthObs = await nasaEarthdataService.AnalyzeParcelAsync(parcel, climateTask.Result, ct);
        earthObs.PestRisk = pestRiskService.Assess(climateTask.Result, "Maíz");

        return Ok(new
        {
            parcel,
            earthObservations = earthObs,
            soil = soilTask.Result
        });
    }

    [HttpGet("precipitation/annual")]
    public async Task<ActionResult<AnnualMonthlyProfileDto>> GetAnnualPrecipitation(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] int year = 2025,
        CancellationToken ct = default)
    {
        var result = await nasaPowerService.GetAnnualMonthlyProfileAsync(lat, lon, year, ct);
        return Ok(result);
    }

    [HttpGet("precipitation/daily")]
    public async Task<ActionResult<MonthlyDailyPrecipitationDto>> GetMonthlyDailyPrecipitation(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] int year = 2025,
        [FromQuery] int month = 5,
        CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
            return BadRequest("El mes debe estar entre 1 y 12.");

        var result = await nasaPowerService.GetDailyPrecipitationForMonthAsync(lat, lon, year, month, ct);
        return Ok(result);
    }
}
