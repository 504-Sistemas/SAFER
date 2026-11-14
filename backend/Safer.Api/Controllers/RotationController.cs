using Microsoft.AspNetCore.Mvc;
using Safer.Api.Models;
using Safer.Api.Services.DecisionEngine;
using Safer.Api.Services.Nasa;
using Safer.Api.Services.Soil;

namespace Safer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RotationController(
    ICropCatalogService cropCatalogService,
    INasaPowerService nasaPowerService,
    INasaEarthdataService nasaEarthdataService,
    ISoilService soilService,
    IRotationEngineService rotationEngineService,
    IPestRiskService pestRiskService) : ControllerBase
{
    [HttpGet("crops")]
    public ActionResult<IReadOnlyList<CropDefinition>> GetCrops()
    {
        return Ok(cropCatalogService.GetAll());
    }

    [HttpPost("recommend")]
    public async Task<ActionResult<DecisionSupportResponseDto>> Recommend(
        [FromBody] RecommendationRequestDto request,
        CancellationToken ct)
    {
        if (request.Parcel is null || request.Parcel.Centroid is null)
            return BadRequest("Se requiere una parcela válida con centroide.");

        var lat = request.Parcel.Centroid.Latitude;
        var lon = request.Parcel.Centroid.Longitude;

        var climateTask = nasaPowerService.GetClimatologyAsync(lat, lon, ct);
        var soilTask = soilService.GetSoilProfileAsync(lat, lon, request.SoilOverrides, ct);

        await Task.WhenAll(climateTask, soilTask);

        var earthObs = await nasaEarthdataService.AnalyzeParcelAsync(request.Parcel, climateTask.Result, ct);
        var baselineCrop = cropCatalogService.GetByIdOrDefault(request.BaselineCropId);

        var (recommendations, scenarios) = rotationEngineService.GenerateRecommendations(
            earthObs,
            soilTask.Result,
            request.Priorities,
            baselineCrop.Id);

        var distinctFamilies = recommendations.Count > 0
            ? recommendations[0].Cycles.Select(c => c.Crop.BotanicalFamily).Distinct().Count()
            : 3;
        earthObs.PestRisk = pestRiskService.Assess(climateTask.Result, baselineCrop.Name, distinctFamilies);

        return Ok(new DecisionSupportResponseDto
        {
            Parcel = request.Parcel,
            EarthObservations = earthObs,
            Soil = soilTask.Result,
            Priorities = request.Priorities,
            BaselineCropName = baselineCrop.Name,
            Recommendations = recommendations,
            WhatIfScenarios = scenarios
        });
    }
}
