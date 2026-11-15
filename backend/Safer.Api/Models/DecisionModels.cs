namespace Safer.Api.Models;

public class FarmerPrioritiesDto
{
    /// <summary>0 to 100 weight for conserving water and tolerating dry spells.</summary>
    public int WaterConservation { get; set; } = 80;
    /// <summary>0 to 100 weight for rebuilding organic matter and nitrogen.</summary>
    public int SoilHealth { get; set; } = 85;
    /// <summary>0 to 100 weight for breaking pest/disease cycles via botanical diversity.</summary>
    public int PestControl { get; set; } = 70;
    /// <summary>0 to 100 weight for farm profitability and market stability.</summary>
    public int EconomicStability { get; set; } = 75;
    /// <summary>0 to 100 weight for resilience against heatwaves and extreme weather.</summary>
    public int ClimateResilience { get; set; } = 80;
}

public class CropDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ScientificName { get; set; } = string.Empty;
    public string BotanicalFamily { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Cereal, Legume, CoverCrop, Tuber, CashCrop
    public int CycleDays { get; set; }
    public double WaterDemandMm { get; set; }
    public double KcCoefficient { get; set; }
    /// <summary>-2 (heavy N feeder) to +3 (strong N fixer).</summary>
    public double NitrogenBalance { get; set; }
    public double OrganicMatterContribution { get; set; } // 0 to 10
    public string RootDepth { get; set; } = "Medium"; // Shallow, Medium, Deep
    public double MinTempC { get; set; }
    public double MaxTempC { get; set; }
    public double MinPh { get; set; }
    public double MaxPh { get; set; }
    public double DroughtTolerance { get; set; } // 0 to 10
    public double HeatTolerance { get; set; }    // 0 to 10
    public double MarketValueIndex { get; set; } // 0 to 10
    public string AgronomicRole { get; set; } = string.Empty;
}

public class RotationCycleStep
{
    public int CycleNumber { get; set; }
    public string SeasonLabel { get; set; } = string.Empty;
    public CropDefinition Crop { get; set; } = new();
    public string Rationale { get; set; } = string.Empty;
    public double DurationMonths { get; set; }
    public string SuggestedWindow { get; set; } = string.Empty;
    public int RestDaysAfter { get; set; }
    public string RestAndFertilizationTip { get; set; } = string.Empty;
}

public class RotationRecommendationDto
{
    public int Rank { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public double WaterEfficiencyScore { get; set; }
    public double SoilHealthScore { get; set; }
    public double PestBreakScore { get; set; }
    public double EconomicScore { get; set; }
    public double ClimateResilienceScore { get; set; }
    public double NetNitrogenBalance { get; set; }
    public double TotalWaterDemandMm { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<RotationCycleStep> Cycles { get; set; } = [];
    public List<ScenarioMetricPoint> WhatIfScenarios { get; set; } = [];
}

public class ScenarioMetricPoint
{
    public string ScenarioName { get; set; } = string.Empty; // Normal, Drought (-25% rain), Heatwave (+2°C)
    public string ScenarioCode { get; set; } = string.Empty;
    public double MonocultureYieldStabilityPct { get; set; }
    public double RotationYieldStabilityPct { get; set; }
    public double MonocultureSoilHealthIndex { get; set; }
    public double RotationSoilHealthIndex { get; set; }
    public double MonocultureWaterUseMm { get; set; }
    public double RotationWaterUseMm { get; set; }
    public double WaterSavedPct { get; set; }
    public string KeyInsight { get; set; } = string.Empty;
}

public class RecommendationRequestDto
{
    public ParsedParcelDto Parcel { get; set; } = new();
    public SoilOverrideDto? SoilOverrides { get; set; }
    public FarmerPrioritiesDto Priorities { get; set; } = new();
    public string BaselineCropId { get; set; } = "maize";
}

public class DecisionSupportResponseDto
{
    public ParsedParcelDto Parcel { get; set; } = new();
    public EarthObservationSummaryDto EarthObservations { get; set; } = new();
    public SoilProfileDto Soil { get; set; } = new();
    public FarmerPrioritiesDto Priorities { get; set; } = new();
    public string BaselineCropName { get; set; } = "Maíz";
    public List<RotationRecommendationDto> Recommendations { get; set; } = [];
    public List<ScenarioMetricPoint> WhatIfScenarios { get; set; } = [];
}
