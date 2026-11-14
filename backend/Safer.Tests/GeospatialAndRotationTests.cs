using Safer.Api.Models;
using Safer.Api.Services.DecisionEngine;
using Safer.Api.Services.Geospatial;

namespace Safer.Tests;

public class GeospatialAndRotationTests
{
    private readonly GeospatialParserService _parser = new();
    private readonly CropCatalogService _catalog = new();

    [Fact]
    public void ParseKml_Finca1_ReturnsValidCentroidBoundingBoxAndArea()
    {
        var kmlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "feadback", "Finca1.kml"));
        Assert.True(File.Exists(kmlPath), $"No se encontró Finca1.kml en {kmlPath}");

        var kmlContent = File.ReadAllText(kmlPath);
        var result = _parser.ParseKml(kmlContent);

        Assert.Equal("Finca1", result.Name);
        Assert.Equal("KML", result.SourceFormat);
        Assert.InRange(result.Centroid.Latitude, 14.50, 14.60);
        Assert.InRange(result.Centroid.Longitude, -88.45, -88.35);
        Assert.True(result.AreaHectares > 10.0 && result.AreaHectares < 500.0, $"Área inesperada: {result.AreaHectares}");
        Assert.Contains("\"Polygon\"", result.GeoJsonFeature);
    }

    [Fact]
    public void ParseGeoJson_Polygon_ComputesCentroidAndArea()
    {
        const string geoJson = """
        {
          "type": "Feature",
          "properties": { "name": "Lote Norte" },
          "geometry": {
            "type": "Polygon",
            "coordinates": [[
              [-87.55, 14.62],
              [-87.54, 14.62],
              [-87.54, 14.63],
              [-87.55, 14.63],
              [-87.55, 14.62]
            ]]
          }
        }
        """;

        var result = _parser.ParseGeoJson(geoJson);

        Assert.Equal("Lote Norte", result.Name);
        Assert.InRange(result.Centroid.Latitude, 14.624, 14.626);
        Assert.InRange(result.Centroid.Longitude, -87.546, -87.544);
        Assert.True(result.AreaHectares > 100.0);
    }

    [Fact]
    public void RotationEngine_ReturnsTop3DistinctStrategies_WithoutConsecutiveSameFamily()
    {
        var engine = new RotationEngineService(_catalog);
        var earthObs = new EarthObservationSummaryDto
        {
            Climate = new NasaPowerClimateDto
            {
                AnnualPrecipitationMm = 1100,
                MeanTemperatureC = 22.5,
                MaxTemperatureC = 29.0,
                MinTemperatureC = 16.0,
                DryMonthsCount = 5,
                WaterDeficitRisk = "Moderate",
                ThermalStressRisk = "Low"
            }
        };
        var soil = new SoilProfileDto
        {
            Ph = 6.2,
            OrganicCarbonPct = 1.6,
            Drainage = "Moderate"
        };
        var priorities = new FarmerPrioritiesDto
        {
            WaterConservation = 90,
            SoilHealth = 90,
            PestControl = 80,
            EconomicStability = 70,
            ClimateResilience = 85
        };

        var (recommendations, scenarios) = engine.GenerateRecommendations(earthObs, soil, priorities, "maize");

        Assert.Equal(3, recommendations.Count);
        foreach (var rec in recommendations)
        {
            Assert.Equal(3, rec.Cycles.Count);
            Assert.NotEqual(rec.Cycles[0].Crop.BotanicalFamily, rec.Cycles[1].Crop.BotanicalFamily);
            Assert.NotEqual(rec.Cycles[1].Crop.BotanicalFamily, rec.Cycles[2].Crop.BotanicalFamily);
        }

        Assert.Equal(3, scenarios.Count);
        Assert.Contains(scenarios, s => s.ScenarioCode == "NORMAL");
        Assert.Contains(scenarios, s => s.ScenarioCode == "DROUGHT");
        Assert.Contains(scenarios, s => s.ScenarioCode == "HEATWAVE");
    }
}
