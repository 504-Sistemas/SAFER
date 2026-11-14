using Safer.Api.Models;

namespace Safer.Api.Services.DecisionEngine;

public interface IRotationEngineService
{
    (List<RotationRecommendationDto> Recommendations, List<ScenarioMetricPoint> Scenarios) GenerateRecommendations(
        EarthObservationSummaryDto earthObs,
        SoilProfileDto soil,
        FarmerPrioritiesDto priorities,
        string baselineCropId);
}

public class RotationEngineService(ICropCatalogService catalogService) : IRotationEngineService
{
    public (List<RotationRecommendationDto> Recommendations, List<ScenarioMetricPoint> Scenarios) GenerateRecommendations(
        EarthObservationSummaryDto earthObs,
        SoilProfileDto soil,
        FarmerPrioritiesDto priorities,
        string baselineCropId)
    {
        var crops = catalogService.GetAll();
        var baselineCrop = catalogService.GetByIdOrDefault(baselineCropId);
        var climate = earthObs.Climate;

        var candidates = new List<RotationRecommendationDto>();

        // Genera 3 ciclos de rotacion: Ciclo 1 (Primera - Wet), Ciclo 2 (Postrera - Transitional), Ciclo 3 (Seca/Cobertura)
        foreach (var c1 in crops.Where(c => c.Category is "Cereal" or "CashCrop" or "Tuber"))
        {
            foreach (var c2 in crops.Where(c => c.BotanicalFamily != c1.BotanicalFamily))
            {
                foreach (var c3 in crops.Where(c => c.BotanicalFamily != c2.BotanicalFamily && (c.Category == "CoverCrop" || c.DroughtTolerance >= 7.0)))
                {
                    var seq = new[] { c1, c2, c3 };
                    var scored = ScoreSequence(seq, climate, soil, priorities, earthObs.Terrain);
                    candidates.Add(scored);
                }
            }
        }

        // Select diverse top 3 (distinct crop sets so #1, #2, #3 are genuinely different strategies)
        var top3 = new List<RotationRecommendationDto>();
        var seenSets = new HashSet<string>();
        foreach (var candidate in candidates.OrderByDescending(c => c.OverallScore))
        {
            var setKey = string.Join("|", candidate.Cycles.Select(c => c.Crop.Id).OrderBy(id => id));
            if (!seenSets.Add(setKey))
                continue;

            candidate.Rank = top3.Count + 1;
            candidate.WhatIfScenarios = BuildWhatIfScenarios(baselineCrop, candidate, climate, soil);
            top3.Add(candidate);
            if (top3.Count == 3) break;
        }

        var scenarios = top3.Count > 0 ? top3[0].WhatIfScenarios : [];

        return (top3, scenarios);
    }

    private static RotationRecommendationDto ScoreSequence(
        CropDefinition[] seq,
        NasaPowerClimateDto climate,
        SoilProfileDto soil,
        FarmerPrioritiesDto priorities,
        ParcelTerrainDto? terrain)
    {
        var avgDailyEt0 = climate.AnnualEt0Mm > 0 ? climate.AnnualEt0Mm / 365.0 : 4.0;
        var totalWater = seq.Sum(c => Math.Round(avgDailyEt0 * c.CycleDays * c.KcCoefficient, 0));
        var netN = Math.Round(seq.Sum(c => c.NitrogenBalance), 1);
        var avgOm = seq.Average(c => c.OrganicMatterContribution);
        var avgDrought = seq.Average(c => c.DroughtTolerance);
        var avgHeat = seq.Average(c => c.HeatTolerance);
        var avgMarket = seq.Average(c => c.MarketValueIndex);

        // 1. Water efficiency score (0-100)
        var waterRatio = totalWater / Math.Max(climate.AnnualPrecipitationMm, 450.0);
        var waterScore = Math.Clamp(100.0 - (waterRatio - 0.65) * 55.0 + (avgDrought * 2.0), 15.0, 99.0);
        if (climate.WaterDeficitRisk is "High" or "Riesgo Alto" && totalWater > 1250)
            waterScore -= 15.0;

        // 2. Soil health score (0-100): rewards positive/balanced N, organic matter, root diversity & slope cover
        var distinctRoots = seq.Select(c => c.RootDepth).Distinct().Count();
        var slopeCoverBonus = terrain is { EstimatedSlopePct: >= 8.0 } && seq.Any(c => c.Category == "CoverCrop") ? 6.0 : 0.0;
        var soilScore = Math.Clamp(
            42.0 + (netN * 7.5) + (avgOm * 4.2) + (distinctRoots * 5.5) + slopeCoverBonus + (soil.OrganicCarbonPct < 1.5 && netN > 0 ? 8.0 : 0.0),
            15.0, 99.0);

        // 3. Pest break score (0-100): 3 distinct families = high score
        var distinctFamilies = seq.Select(c => c.BotanicalFamily).Distinct().Count();
        var pestScore = distinctFamilies == 3 ? 95.0 : distinctFamilies == 2 ? 75.0 : 40.0;

        // 4. Economic score (0-100)
        var econScore = Math.Clamp(avgMarket * 10.5 + (seq.Count(c => c.Category != "CoverCrop") * 4.0), 20.0, 98.0);

        // 5. Climate resilience score (0-100)
        var tempFitBonus = seq.All(c => climate.MeanTemperatureC >= c.MinTempC && climate.MaxTemperatureC <= c.MaxTempC + 2.0) ? 10.0 : -8.0;
        var phFitBonus = seq.All(c => soil.Ph >= c.MinPh - 0.3 && soil.Ph <= c.MaxPh + 0.3) ? 6.0 : -10.0;
        var resilienceScore = Math.Clamp((avgDrought * 5.0) + (avgHeat * 4.5) + tempFitBonus + phFitBonus, 15.0, 99.0);

        // Weighted MCDA total
        var wWater = Math.Max(priorities.WaterConservation, 1);
        var wSoil = Math.Max(priorities.SoilHealth, 1);
        var wPest = Math.Max(priorities.PestControl, 1);
        var wEcon = Math.Max(priorities.EconomicStability, 1);
        var wClim = Math.Max(priorities.ClimateResilience, 1);
        var wSum = (double)(wWater + wSoil + wPest + wEcon + wClim);

        var overall = (
            waterScore * wWater +
            soilScore * wSoil +
            pestScore * wPest +
            econScore * wEcon +
            resilienceScore * wClim
        ) / wSum;

        var strategyName = $"{seq[0].Name} → {seq[1].Name} → {seq[2].Name}";
        var nitrogenText = netN > 0 ? "ayuda a abonar la tierra naturalmente" : netN == 0 ? "mantiene el abono equilibrado" : "requiere apoyo de abono";
        var summary = $"Combina {distinctFamilies} tipos de plantas distintas ({nitrogenText}) y necesita ~{totalWater:F0} mm de agua frente a {climate.AnnualPrecipitationMm:F0} mm de lluvia al año.";

        var seasons = new[] { "1ª Siembra (Época de Lluvias)", "2ª Siembra (Postrera)", "3ª Siembra (Época Seca / Cobertura)" };
        var cycles = seq.Select((crop, idx) => new RotationCycleStep
        {
            CycleNumber = idx + 1,
            SeasonLabel = seasons[idx],
            Crop = crop,
            Rationale = BuildStepRationale(crop, idx, climate, soil)
        }).ToList();

        return new RotationRecommendationDto
        {
            StrategyName = strategyName,
            OverallScore = Math.Round(overall, 1),
            WaterEfficiencyScore = Math.Round(waterScore, 1),
            SoilHealthScore = Math.Round(soilScore, 1),
            PestBreakScore = Math.Round(pestScore, 1),
            EconomicScore = Math.Round(econScore, 1),
            ClimateResilienceScore = Math.Round(resilienceScore, 1),
            NetNitrogenBalance = netN,
            TotalWaterDemandMm = Math.Round(totalWater, 0),
            Summary = summary,
            Cycles = cycles
        };
    }

    private static string BuildStepRationale(CropDefinition crop, int idx, NasaPowerClimateDto climate, SoilProfileDto soil)
    {
        var avgDailyEt0 = climate.AnnualEt0Mm > 0 ? climate.AnnualEt0Mm / 365.0 : 4.0;
        var cropEtcMm = Math.Round(avgDailyEt0 * crop.CycleDays * crop.KcCoefficient, 0);

        if (idx == 0)
            return $"Aprovecha las lluvias fuertes ({climate.AnnualPrecipitationMm:F0} mm/año); necesita ~{cropEtcMm:F0} mm de agua y se adapta bien a tu tierra (pH {soil.Ph:F1}).";
        if (idx == 1)
            return $"Cambia a otro tipo de planta para cortar plagas (necesita ~{cropEtcMm:F0} mm); {(crop.NitrogenBalance > 0 ? "ayuda a fertilizar el suelo" : "aprovecha la humedad que quedó en la tierra")}.";
        return $"Cierra el año con {crop.Name} (necesita ~{cropEtcMm:F0} mm, aguanta bien la falta de agua) para cubrir y proteger tu tierra.";
    }

    private static List<ScenarioMetricPoint> BuildWhatIfScenarios(
        CropDefinition baselineCrop,
        RotationRecommendationDto bestRotation,
        NasaPowerClimateDto climate,
        SoilProfileDto soil)
    {
        var avgDailyEt0 = climate.AnnualEt0Mm > 0 ? climate.AnnualEt0Mm / 365.0 : 4.0;
        var monoWater = Math.Round(avgDailyEt0 * baselineCrop.CycleDays * baselineCrop.KcCoefficient * 3.0, 0);
        var rotWater = bestRotation.TotalWaterDemandMm;
        var waterSavedPct = Math.Round(Math.Clamp((monoWater - rotWater) / Math.Max(monoWater, 1.0) * 100.0, -15.0, 60.0), 1);

        var baseSoil = Math.Clamp(45.0 + soil.OrganicCarbonPct * 12.0, 35.0, 75.0);

        return
        [
            new ScenarioMetricPoint
            {
                ScenarioCode = "NORMAL",
                ScenarioName = "Año Normal de Lluvias",
                MonocultureYieldStabilityPct = 76.0,
                RotationYieldStabilityPct = Math.Round(Math.Min(95.0, 78.0 + bestRotation.ClimateResilienceScore * 0.16), 1),
                MonocultureSoilHealthIndex = Math.Round(Math.Max(25.0, baseSoil - 8.0), 1),
                RotationSoilHealthIndex = Math.Round(Math.Min(96.0, baseSoil + (bestRotation.SoilHealthScore * 0.35)), 1),
                MonocultureWaterUseMm = monoWater,
                RotationWaterUseMm = rotWater,
                WaterSavedPct = waterSavedPct,
                KeyInsight = $"En un año normal, este plan cuida mejor tu tierra y ahorra {Math.Max(0, waterSavedPct):F1}% de agua frente a sembrar siempre {baselineCrop.Name}."
            },
            new ScenarioMetricPoint
            {
                ScenarioCode = "DROUGHT",
                ScenarioName = "Si hay Sequía fuerte (-25% lluvia)",
                MonocultureYieldStabilityPct = Math.Round(Math.Clamp(42.0 + baselineCrop.DroughtTolerance * 2.5, 30.0, 72.0), 1),
                RotationYieldStabilityPct = Math.Round(Math.Clamp(64.0 + bestRotation.WaterEfficiencyScore * 0.25, 65.0, 92.0), 1),
                MonocultureSoilHealthIndex = Math.Round(Math.Max(20.0, baseSoil - 15.0), 1),
                RotationSoilHealthIndex = Math.Round(Math.Min(94.0, baseSoil + (bestRotation.SoilHealthScore * 0.28)), 1),
                MonocultureWaterUseMm = monoWater,
                RotationWaterUseMm = Math.Round(rotWater * 0.92, 0),
                WaterSavedPct = Math.Round(Math.Max(waterSavedPct, waterSavedPct + 6.0), 1),
                KeyInsight = "Dejar el terreno cubierto en época seca y alternar raíces evita que la tierra pierda humedad cuando falta la lluvia."
            },
            new ScenarioMetricPoint
            {
                ScenarioCode = "HEATWAVE",
                ScenarioName = "Si hace mucho Calor (+2°C)",
                MonocultureYieldStabilityPct = Math.Round(Math.Clamp(45.0 + baselineCrop.HeatTolerance * 2.4, 32.0, 74.0), 1),
                RotationYieldStabilityPct = Math.Round(Math.Clamp(66.0 + bestRotation.ClimateResilienceScore * 0.24, 66.0, 91.0), 1),
                MonocultureSoilHealthIndex = Math.Round(Math.Max(22.0, baseSoil - 12.0), 1),
                RotationSoilHealthIndex = Math.Round(Math.Min(93.0, baseSoil + (bestRotation.SoilHealthScore * 0.30)), 1),
                MonocultureWaterUseMm = Math.Round(monoWater * 1.08, 0),
                RotationWaterUseMm = Math.Round(rotWater * 1.02, 0),
                WaterSavedPct = Math.Round(Math.Max(waterSavedPct, waterSavedPct + 4.0), 1),
                KeyInsight = $"Si el calor sube hasta {climate.MaxTemperatureC + 2.0:F1}°C, variar de cultivos evita que toda tu cosecha se pierda al mismo tiempo."
            }
        ];
    }
}
