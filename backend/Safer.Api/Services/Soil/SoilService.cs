using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Safer.Api.Models;

namespace Safer.Api.Services.Soil;

public interface ISoilService
{
    Task<SoilProfileDto> GetSoilProfileAsync(double latitude, double longitude, SoilOverrideDto? overrides = null, CancellationToken ct = default);
}

public class SoilService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<SoilService> logger) : ISoilService
{
    public async Task<SoilProfileDto> GetSoilProfileAsync(
        double latitude,
        double longitude,
        SoilOverrideDto? overrides = null,
        CancellationToken ct = default)
    {
        var baseProfile = await FetchSoilGridsBaselineAsync(latitude, longitude, ct);

        if (overrides is null)
            return baseProfile;

        var merged = new SoilProfileDto
        {
            Source = baseProfile.IsLiveApi
                ? "ISRIC SoilGrids + Ajuste Local del Agricultor"
                : "Línea Base Regional + Ajuste Local del Agricultor",
            IsLiveApi = baseProfile.IsLiveApi,
            TextureClass = string.IsNullOrWhiteSpace(overrides.TextureClass) ? baseProfile.TextureClass : overrides.TextureClass!,
            ClayPct = overrides.ClayPct ?? baseProfile.ClayPct,
            SandPct = overrides.SandPct ?? baseProfile.SandPct,
            SiltPct = overrides.SiltPct ?? baseProfile.SiltPct,
            Ph = overrides.Ph ?? baseProfile.Ph,
            OrganicCarbonPct = overrides.OrganicCarbonPct ?? baseProfile.OrganicCarbonPct,
            NitrogenLevel = string.IsNullOrWhiteSpace(overrides.NitrogenLevel) ? baseProfile.NitrogenLevel : overrides.NitrogenLevel!,
            Drainage = string.IsNullOrWhiteSpace(overrides.Drainage) ? baseProfile.Drainage : overrides.Drainage!
        };

        return merged;
    }

    private async Task<SoilProfileDto> FetchSoilGridsBaselineAsync(double latitude, double longitude, CancellationToken ct)
    {
        var cacheKey = $"soilgrids_{Math.Round(latitude, 2)}_{Math.Round(longitude, 2)}";
        if (cache.TryGetValue(cacheKey, out SoilProfileDto? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://rest.isric.org/soilgrids/v2.0/properties/query?lon={0:F4}&lat={1:F4}&property=clay&property=sand&property=silt&property=phh2o&property=soc&depth=0-5cm&value=mean",
                longitude,
                latitude);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var response = await httpClient.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var layers = doc.RootElement.GetProperty("properties").GetProperty("layers");
            double clay = 30, sand = 36, silt = 34, ph = 6.2, soc = 1.8;

            foreach (var layer in layers.EnumerateArray())
            {
                var name = layer.GetProperty("name").GetString();
                var meanVal = layer.GetProperty("depths")[0].GetProperty("values").GetProperty("mean");
                if (meanVal.ValueKind != JsonValueKind.Number) continue;
                var raw = meanVal.GetDouble();

                switch (name)
                {
                    case "clay": clay = Math.Round(raw / 10.0, 1); break;     // g/kg -> %
                    case "sand": sand = Math.Round(raw / 10.0, 1); break;     // g/kg -> %
                    case "silt": silt = Math.Round(raw / 10.0, 1); break;     // g/kg -> %
                    case "phh2o": ph = Math.Round(raw / 10.0, 1); break;      // pH*10 -> pH
                    case "soc": soc = Math.Round(raw / 100.0, 2); break;      // dg/kg -> %
                }
            }

            var texture = ClassifyTexture(clay, sand);
            var drainage = sand >= 55 ? "Buen drenaje" : clay >= 40 ? "Se encharca fácil" : "Drenaje normal";
            var nitrogen = soc >= 2.2 ? "High" : soc >= 1.3 ? "Medium" : "Low";

            var profile = new SoilProfileDto
            {
                Source = "ISRIC SoilGrids v2.0 (Live)",
                IsLiveApi = true,
                TextureClass = texture,
                ClayPct = clay,
                SandPct = sand,
                SiltPct = silt,
                Ph = ph,
                OrganicCarbonPct = soc,
                Drainage = drainage,
                NitrogenLevel = nitrogen
            };

            cache.Set(cacheKey, profile, TimeSpan.FromHours(12));
            return profile;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SoilGrids no disponible/timeout para ({Lat}, {Lon}). Usando línea base regional.", latitude, longitude);
            return new SoilProfileDto
            {
                Source = "Línea Base Regional (Editable por agricultor)",
                IsLiveApi = false,
                TextureClass = "Franco Arcilloso",
                ClayPct = 31.0,
                SandPct = 35.0,
                SiltPct = 34.0,
                Ph = 6.1,
                OrganicCarbonPct = 1.75,
                NitrogenLevel = "Medium",
                Drainage = "Drenaje normal"
            };
        }
    }

    private static string ClassifyTexture(double clay, double sand)
    {
        if (clay >= 40) return "Arcilloso";
        if (sand >= 60) return "Franco Arenoso";
        if (clay >= 27) return "Franco Arcilloso";
        return "Franco";
    }
}
