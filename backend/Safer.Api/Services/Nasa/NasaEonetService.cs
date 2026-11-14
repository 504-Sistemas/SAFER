using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Safer.Api.Models;

namespace Safer.Api.Services.Nasa;

public interface INasaEonetService
{
    Task<(List<EonetEventDto> Events, bool IsLive)> GetRegionalEventsAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default);
}

public class NasaEonetService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<NasaEonetService> logger) : INasaEonetService
{
    public async Task<(List<EonetEventDto> Events, bool IsLive)> GetRegionalEventsAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        var cacheKey = $"eonet_50km_{Math.Round(latitude, 1)}_{Math.Round(longitude, 1)}";
        if (cache.TryGetValue(cacheKey, out List<EonetEventDto>? cached) && cached is not null)
        {
            return (cached, true);
        }

        try
        {
            const double delta = 0.6; // ~65 km local search box
            var minLon = longitude - delta;
            var maxLon = longitude + delta;
            var minLat = latitude - delta;
            var maxLat = latitude + delta;

            // EONET v3 bbox format: minLon,maxLat,maxLon,minLat
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://eonet.gsfc.nasa.gov/api/v3/events?bbox={0:F2},{1:F2},{2:F2},{3:F2}&status=all&days=180&limit=8",
                minLon, maxLat, maxLon, minLat);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var response = await httpClient.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var list = new List<EonetEventDto>();
            if (doc.RootElement.TryGetProperty("events", out var eventsEl))
            {
                foreach (var ev in eventsEl.EnumerateArray())
                {
                    var id = ev.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    var title = ev.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "";
                    var link = ev.TryGetProperty("link", out var lEl) ? lEl.GetString() : null;
                    var closed = ev.TryGetProperty("closed", out var cEl) && cEl.ValueKind != JsonValueKind.Null;

                    var category = "Natural Event";
                    if (ev.TryGetProperty("categories", out var cats) && cats.GetArrayLength() > 0)
                    {
                        category = cats[0].GetProperty("title").GetString() ?? category;
                    }

                    DateTimeOffset date = DateTimeOffset.UtcNow;
                    double distanceKm = 0;

                    if (ev.TryGetProperty("geometry", out var geoms) && geoms.GetArrayLength() > 0)
                    {
                        var latest = geoms[geoms.GetArrayLength() - 1];
                        if (latest.TryGetProperty("date", out var dEl) && DateTimeOffset.TryParse(dEl.GetString(), out var parsedDate))
                        {
                            date = parsedDate;
                        }
                        if (latest.TryGetProperty("coordinates", out var coords) &&
                            coords.ValueKind == JsonValueKind.Array &&
                            coords.GetArrayLength() >= 1)
                        {
                            if (coords.GetArrayLength() >= 2 &&
                                coords[0].ValueKind == JsonValueKind.Number &&
                                coords[1].ValueKind == JsonValueKind.Number)
                            {
                                distanceKm = Math.Round(HaversineKm(latitude, longitude, coords[1].GetDouble(), coords[0].GetDouble()), 1);
                            }
                            else if (coords[0].ValueKind == JsonValueKind.Array && coords[0].GetArrayLength() > 0)
                            {
                                // Polygon geometry: [[[lon, lat], ...]]
                                var ring = coords[0];
                                var firstPt = ring[0];
                                if (firstPt.ValueKind == JsonValueKind.Array && firstPt.GetArrayLength() >= 2 &&
                                    firstPt[0].ValueKind == JsonValueKind.Number && firstPt[1].ValueKind == JsonValueKind.Number)
                                {
                                    distanceKm = Math.Round(HaversineKm(latitude, longitude, firstPt[1].GetDouble(), firstPt[0].GetDouble()), 1);
                                }
                            }
                        }
                    }

                    list.Add(new EonetEventDto
                    {
                        Id = id,
                        Title = HumanizeTitle(title),
                        Category = TranslateCategory(category),
                        Date = date,
                        DistanceKm = distanceKm,
                        Status = closed ? "Evento pasado" : "Vigente ahora",
                        SourceUrl = link
                    });
                }
            }

            var ordered = list
                .Where(e => e.DistanceKm > 0 && e.DistanceKm <= 50)
                .OrderBy(e => e.DistanceKm)
                .Take(5)
                .ToList();
            cache.Set(cacheKey, ordered, TimeSpan.FromHours(2));
            return (ordered, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al consultar NASA EONET v3 para ({Lat}, {Lon}).", latitude, longitude);
            return ([], false);
        }
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static string TranslateCategory(string category) => category switch
    {
        "Floods" => "Inundación",
        "Wildfires" => "Incendio",
        "Severe Storms" => "Tormenta fuerte",
        "Drought" => "Sequía",
        "Landslides" => "Deslizamiento",
        _ => "Alerta climática"
    };

    private static string HumanizeTitle(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return "Evento climático en la región";

        var clean = System.Text.RegularExpressions.Regex.Replace(rawTitle, @"\s+\d{5,}$", "").Trim();
        clean = clean
            .Replace("Flood in ", "Inundación en ", StringComparison.OrdinalIgnoreCase)
            .Replace("Wildfire in ", "Incendio en ", StringComparison.OrdinalIgnoreCase)
            .Replace("Fire in ", "Incendio en ", StringComparison.OrdinalIgnoreCase)
            .Replace("Severe Storm ", "Tormenta fuerte ", StringComparison.OrdinalIgnoreCase)
            .Replace("Tropical Storm ", "Tormenta tropical ", StringComparison.OrdinalIgnoreCase)
            .Replace("Hurricane ", "Huracán ", StringComparison.OrdinalIgnoreCase)
            .Replace("Drought in ", "Sequía en ", StringComparison.OrdinalIgnoreCase);

        return clean;
    }
}
