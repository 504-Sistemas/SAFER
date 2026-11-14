using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Safer.Api.Models;
using Safer.Api.Services.Geospatial;

namespace Safer.Api.Services.Nasa;

public interface INasaEarthdataService
{
    Task<EarthObservationSummaryDto> AnalyzeParcelAsync(
        ParsedParcelDto parcel,
        NasaPowerClimateDto climate,
        CancellationToken ct = default);
}

public class NasaEarthdataService(
    HttpClient httpClient,
    IMemoryCache cache,
    IConfiguration configuration,
    INasaEonetService eonetService,
    IElevationService elevationService,
    INasaPowerService nasaPowerService,
    ILogger<NasaEarthdataService> logger) : INasaEarthdataService
{
    private const string CmrStacSearchUrl = "https://cmr.earthdata.nasa.gov/stac/LPCLOUD/search";
    private const string AsfStacSearchUrl = "https://cmr.earthdata.nasa.gov/stac/ASF/search";

    public async Task<EarthObservationSummaryDto> AnalyzeParcelAsync(
        ParsedParcelDto parcel,
        NasaPowerClimateDto climate,
        CancellationToken ct = default)
    {
        var token = configuration["NasaEarthdata:Token"]
            ?? Environment.GetEnvironmentVariable("NASA_EARTHDATA_TOKEN");
        var tokenConfigured = !string.IsNullOrWhiteSpace(token);

        var sentinelTask = SearchSentinelHlsScenesAsync(parcel.BoundingBox, token, ct);
        var radarTask = SearchOperaSentinel1RadarAsync(parcel.BoundingBox, token, ct);
        var eonetTask = eonetService.GetRegionalEventsAsync(parcel.Centroid.Latitude, parcel.Centroid.Longitude, ct);
        var terrainTask = elevationService.GetTerrainAsync(parcel, ct);
        var anomalyTask = nasaPowerService.GetRecent30DayAnomalyAsync(parcel.Centroid.Latitude, parcel.Centroid.Longitude, climate, ct);

        await Task.WhenAll(sentinelTask, radarTask, eonetTask, terrainTask, anomalyTask);

        var (scenes, isLive, statusMsg) = sentinelTask.Result;
        var radarScenes = radarTask.Result;
        var (eonetEvents, eonetLive) = eonetTask.Result;

        // Derived baseline vegetation & soil moisture indices informed by NASA POWER water/thermal regime
        var rainFactor = Math.Clamp(climate.AnnualPrecipitationMm / 1600.0, 0.25, 0.95);
        var dryPenalty = climate.DryMonthsCount * 0.03;
        var ndviBaseline = Math.Round(Math.Clamp(0.38 + (rainFactor * 0.42) - dryPenalty, 0.22, 0.82), 2);
        var moistureIndex = Math.Round(Math.Clamp((climate.MeanRelativeHumidityPct / 100.0) * 0.65 + (rainFactor * 0.30) - dryPenalty, 0.18, 0.85), 2);

        return new EarthObservationSummaryDto
        {
            EarthdataTokenConfigured = tokenConfigured,
            SentinelQueryLive = isLive,
            SentinelCollection = "HLSS30.v2.0 (Harmonized Landsat Sentinel-2)",
            SentinelStatusMessage = statusMsg,
            RecentSentinelScenes = scenes,
            RecentRadarScenes = radarScenes,
            EonetQueryLive = eonetLive,
            RegionalExtremeEvents = eonetEvents,
            Terrain = terrainTask.Result,
            RecentAnomaly = anomalyTask.Result,
            Climate = climate,
            EstimatedNdviBaseline = ndviBaseline,
            EstimatedSoilMoistureIndex = moistureIndex
        };
    }

    private async Task<(List<SentinelSceneDto> Scenes, bool IsLive, string StatusMessage)> SearchSentinelHlsScenesAsync(
        BoundingBox bbox,
        string? token,
        CancellationToken ct)
    {
        var cacheKey = $"cmr_hlss30_{Math.Round(bbox.MinLon, 2)}_{Math.Round(bbox.MinLat, 2)}_{Math.Round(bbox.MaxLon, 2)}_{Math.Round(bbox.MaxLat, 2)}";
        if (cache.TryGetValue(cacheKey, out List<SentinelSceneDto>? cached) && cached is not null)
        {
            return (cached, true, $"Catálogo NASA CMR STAC en vivo ({cached.Count} escenas HLSS30 cacheadas).");
        }

        try
        {
            var payload = new
            {
                collections = new[] { "HLSS30.v2.0" },
                bbox = new[] { bbox.MinLon, bbox.MinLat, bbox.MaxLon, bbox.MaxLat },
                limit = 6
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, CmrStacSearchUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var scenes = new List<SentinelSceneDto>();
            if (doc.RootElement.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    var id = feature.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    var props = feature.GetProperty("properties");

                    var dt = props.TryGetProperty("datetime", out var dtEl) &&
                             DateTimeOffset.TryParse(dtEl.GetString(), out var parsedDt)
                        ? parsedDt
                        : DateTimeOffset.UtcNow;

                    double? cloud = props.TryGetProperty("eo:cloud_cover", out var ccEl) && ccEl.ValueKind == JsonValueKind.Number
                        ? Math.Round(ccEl.GetDouble(), 1)
                        : null;

                    string? browseUrl = null;
                    if (feature.TryGetProperty("assets", out var assets) &&
                        assets.TryGetProperty("browse", out var browse) &&
                        browse.TryGetProperty("href", out var hrefEl))
                    {
                        browseUrl = hrefEl.GetString();
                    }

                    string? selfUrl = null;
                    if (feature.TryGetProperty("links", out var links))
                    {
                        foreach (var link in links.EnumerateArray())
                        {
                            if (link.TryGetProperty("rel", out var relEl) && relEl.GetString() == "self" &&
                                link.TryGetProperty("href", out var linkHref))
                            {
                                selfUrl = linkHref.GetString();
                                break;
                            }
                        }
                    }

                    scenes.Add(new SentinelSceneDto
                    {
                        SceneId = id,
                        Collection = "HLSS30.v2.0",
                        Platform = "Sentinel-2A/2B (HLS)",
                        AcquiredAt = dt,
                        CloudCoverPct = cloud,
                        BrowseImageUrl = browseUrl,
                        StacSelfUrl = selfUrl
                    });
                }
            }

            cache.Set(cacheKey, scenes, TimeSpan.FromHours(2));
            return (scenes, true, $"Consulta exitosa a NASA CMR STAC: {scenes.Count} escenas Sentinel-2 (HLSS30) encontradas.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al consultar NASA CMR STAC HLSS30.");
            return ([], false, "No se pudo conectar con NASA CMR STAC en este momento; usando métricas climáticas y línea base.");
        }
    }

    private async Task<List<SentinelSceneDto>> SearchOperaSentinel1RadarAsync(
        BoundingBox bbox,
        string? token,
        CancellationToken ct)
    {
        var cacheKey = $"cmr_opera_s1_{Math.Round(bbox.MinLon, 2)}_{Math.Round(bbox.MinLat, 2)}_{Math.Round(bbox.MaxLon, 2)}_{Math.Round(bbox.MaxLat, 2)}";
        if (cache.TryGetValue(cacheKey, out List<SentinelSceneDto>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var payload = new
            {
                collections = new[] { "OPERA_L2_RTC-S1_V1" },
                bbox = new[] { bbox.MinLon, bbox.MinLat, bbox.MaxLon, bbox.MaxLat },
                limit = 4
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, AsfStacSearchUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            using var response = await httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var scenes = new List<SentinelSceneDto>();
            if (doc.RootElement.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    var id = feature.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    var props = feature.GetProperty("properties");
                    var dt = props.TryGetProperty("datetime", out var dtEl) &&
                             DateTimeOffset.TryParse(dtEl.GetString(), out var parsedDt)
                        ? parsedDt
                        : DateTimeOffset.UtcNow;

                    scenes.Add(new SentinelSceneDto
                    {
                        SceneId = id,
                        Collection = "OPERA_L2_RTC-S1_V1",
                        Platform = "Sentinel-1 (Radar OPERA)",
                        AcquiredAt = dt,
                        CloudCoverPct = null
                    });
                }
            }

            cache.Set(cacheKey, scenes, TimeSpan.FromHours(2));
            return scenes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al consultar NASA OPERA Sentinel-1 STAC.");
            return [];
        }
    }
}
