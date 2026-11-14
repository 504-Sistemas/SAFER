namespace Safer.Api.Models;

public class MonthlyClimatePoint
{
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public double PrecipitationMm { get; set; }
    public double TempMeanC { get; set; }
    public double TempMaxC { get; set; }
    public double TempMinC { get; set; }
    public double SolarRadiationKwhM2Day { get; set; }
    public double RelativeHumidityPct { get; set; }
    public double ReferenceEt0Mm { get; set; }
    public double WaterBalanceMm { get; set; }
}

public class NasaPowerClimateDto
{
    public string Source { get; set; } = "NASA POWER Climatology API";
    public bool IsLiveApi { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AnnualPrecipitationMm { get; set; }
    public double AnnualEt0Mm { get; set; }
    public double AnnualWaterBalanceMm { get; set; }
    public double MeanTemperatureC { get; set; }
    public double MaxTemperatureC { get; set; }
    public double MinTemperatureC { get; set; }
    public double MeanSolarRadiationKwhM2Day { get; set; }
    public double MeanRelativeHumidityPct { get; set; }
    public int DryMonthsCount { get; set; }
    public string WaterDeficitRisk { get; set; } = "Moderate"; // Low, Moderate, High
    public string ThermalStressRisk { get; set; } = "Low";    // Low, Moderate, High
    public List<MonthlyClimatePoint> MonthlyProfile { get; set; } = [];
}

public class SentinelSceneDto
{
    public string SceneId { get; set; } = string.Empty;
    public string Collection { get; set; } = "HLSS30.v2.0";
    public string Platform { get; set; } = "Sentinel-2 (HLS)";
    public DateTimeOffset AcquiredAt { get; set; }
    public double? CloudCoverPct { get; set; }
    public string? BrowseImageUrl { get; set; }
    public string? StacSelfUrl { get; set; }
}

public class EarthObservationSummaryDto
{
    public bool EarthdataTokenConfigured { get; set; }
    public bool SentinelQueryLive { get; set; }
    public string SentinelCollection { get; set; } = "HLSS30.v2.0 (Harmonized Landsat Sentinel-2)";
    public string? SentinelStatusMessage { get; set; }
    public List<SentinelSceneDto> RecentSentinelScenes { get; set; } = [];
    public List<SentinelSceneDto> RecentRadarScenes { get; set; } = [];
    public bool EonetQueryLive { get; set; }
    public List<EonetEventDto> RegionalExtremeEvents { get; set; } = [];
    public PestRiskAssessmentDto? PestRisk { get; set; }
    public ParcelTerrainDto? Terrain { get; set; }
    public RecentWeatherAnomalyDto? RecentAnomaly { get; set; }
    public NasaPowerClimateDto Climate { get; set; } = new();
    public double EstimatedNdviBaseline { get; set; }
    public double EstimatedSoilMoistureIndex { get; set; }
    public List<string> RecommendedGibsLayers { get; set; } =
    [
        "MODIS_Terra_NDVI_8Day",
        "IMERG_Precipitation_Rate",
        "SMAP_L4_Analyzed_Surface_Soil_Moisture",
        "MODIS_Terra_Land_Surface_Temp_Day"
    ];
}

public class DailyPrecipitationPoint
{
    public string Date { get; set; } = string.Empty;
    public int DayOfMonth { get; set; }
    public double PrecipitationMm { get; set; }
    public double TempMeanC { get; set; }
}

public class AnnualMonthlyProfileDto
{
    public int Year { get; set; }
    public bool IsLiveApi { get; set; }
    public string Source { get; set; } = "NASA POWER Daily API";
    public List<MonthlyClimatePoint> MonthlyProfile { get; set; } = [];
}

public class MonthlyDailyPrecipitationDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public bool IsLiveApi { get; set; }
    public string Source { get; set; } = "NASA POWER Daily API";
    public double TotalPrecipitationMm { get; set; }
    public int RainyDaysCount { get; set; }
    public List<DailyPrecipitationPoint> Days { get; set; } = [];
}

public class EonetEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset Date { get; set; }
    public double DistanceKm { get; set; }
    public string Status { get; set; } = "Activo";
    public string? SourceUrl { get; set; }
}

public class PestRiskAssessmentDto
{
    public string FungalDiseaseRisk { get; set; } = "Moderado";
    public double FungalRiskScore { get; set; }
    public string InsectPestRisk { get; set; } = "Moderado";
    public double InsectRiskScore { get; set; }
    public List<string> HighRiskMonths { get; set; } = [];
    public string MonocultureRiskNote { get; set; } = string.Empty;
    public double RotationRiskReductionPct { get; set; }
    public string ScientificDisclaimer { get; set; } =
        "El satélite detecta humedad, calor y estrés vegetal favorables para plagas y hongos, no insectos directamente. Confirma con inspección visual en tu lote.";
}

public class ParcelTerrainDto
{
    public string Source { get; set; } = "SRTM / Copernicus DEM (30m-90m)";
    public bool IsLiveApi { get; set; }
    public double MeanElevationM { get; set; }
    public double MinElevationM { get; set; }
    public double MaxElevationM { get; set; }
    public double EstimatedSlopePct { get; set; }
    public string ErosionRisk { get; set; } = "Moderado"; // Bajo, Moderado, Alto
    public string TerrainNote { get; set; } = string.Empty;
    public int GridSize { get; set; } = 8;
    public List<double> ElevationGrid { get; set; } = [];
}

public class RecentWeatherAnomalyDto
{
    public bool IsLiveApi { get; set; }
    public double Precipitation30DaysMm { get; set; }
    public double Normal30DaysMm { get; set; }
    public double AnomalyPct { get; set; }
    public double MeanTemp30DaysC { get; set; }
    public string StatusLabel { get; set; } = "Normal"; // Más seco de lo normal, Normal, Más húmedo de lo normal
    public string SummaryText { get; set; } = string.Empty;
}
