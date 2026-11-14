using Safer.Api.Models;

namespace Safer.Api.Services.DecisionEngine;

public interface IPestRiskService
{
    PestRiskAssessmentDto Assess(NasaPowerClimateDto climate, string baselineCropName, int distinctFamiliesInTopRotation = 3);
}

public class PestRiskService : IPestRiskService
{
    public PestRiskAssessmentDto Assess(
        NasaPowerClimateDto climate,
        string baselineCropName,
        int distinctFamiliesInTopRotation = 3)
    {
        var monthly = climate.MonthlyProfile;
        if (monthly.Count == 0)
        {
            return new PestRiskAssessmentDto
            {
                FungalDiseaseRisk = "Moderado",
                FungalRiskScore = 50,
                InsectPestRisk = "Moderado",
                InsectRiskScore = 50,
                HighRiskMonths = ["May", "Jun", "Sep", "Oct"],
                MonocultureRiskNote = $"Repetir {baselineCropName} en el mismo lote mantiene hospederos activos.",
                RotationRiskReductionPct = 40
            };
        }

        var fungalMonths = monthly
            .Where(m => m.RelativeHumidityPct >= 74 && m.PrecipitationMm >= 95 && m.TempMeanC is >= 17 and <= 29.5)
            .ToList();

        var insectMonths = monthly
            .Where(m => m.TempMaxC >= 28.5 && (m.PrecipitationMm < 90 || m.TempMeanC >= 23.5))
            .ToList();

        var fungalScore = Math.Round(Math.Clamp((fungalMonths.Count / 12.0) * 100.0 + (climate.MeanRelativeHumidityPct >= 75 ? 12 : 0), 12, 95), 0);
        var insectScore = Math.Round(Math.Clamp((insectMonths.Count / 12.0) * 100.0 + (climate.MaxTemperatureC >= 30 ? 15 : 0), 15, 95), 0);

        var criticalMonths = fungalMonths
            .Concat(insectMonths)
            .DistinctBy(m => m.Month)
            .OrderBy(m => m.Month)
            .Select(m => m.MonthName)
            .ToList();

        var reductionPct = distinctFamiliesInTopRotation >= 3 ? 48.0 : distinctFamiliesInTopRotation == 2 ? 30.0 : 10.0;

        return new PestRiskAssessmentDto
        {
            FungalDiseaseRisk = ToRiskLabel(fungalScore),
            FungalRiskScore = fungalScore,
            InsectPestRisk = ToRiskLabel(insectScore),
            InsectRiskScore = insectScore,
            HighRiskMonths = criticalMonths,
            MonocultureRiskNote = $"Si mantienes monocultivo de {baselineCropName}, las plagas y esporas sobreviven entre ciclos. Alternar {distinctFamiliesInTopRotation} familias botánicas corta su ciclo reproductivo.",
            RotationRiskReductionPct = reductionPct
        };
    }

    private static string ToRiskLabel(double score) => score >= 65 ? "Alto" : score >= 40 ? "Moderado" : "Bajo";
}
