using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Safer.Api.Models;

namespace Safer.Api.Services.Geospatial;

public interface IElevationService
{
    Task<ParcelTerrainDto> GetTerrainAsync(ParsedParcelDto parcel, CancellationToken ct = default);
}

public class ElevationService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<ElevationService> logger) : IElevationService
{
    private const int TerrainGridSize = 8;

    public async Task<ParcelTerrainDto> GetTerrainAsync(ParsedParcelDto parcel, CancellationToken ct = default)
    {
        var lat = parcel.Centroid.Latitude;
        var lon = parcel.Centroid.Longitude;
        var minLat = parcel.BoundingBox.MinLat;
        var maxLat = parcel.BoundingBox.MaxLat;
        var minLon = parcel.BoundingBox.MinLon;
        var maxLon = parcel.BoundingBox.MaxLon;

        var cacheKey = $"terrain_v2_{Math.Round(lat, 3)}_{Math.Round(lon, 3)}_{Math.Round(minLat, 3)}_{Math.Round(maxLat, 3)}";

        if (cache.TryGetValue(cacheKey, out ParcelTerrainDto? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            // Sample an 8x8 SRTM/Copernicus DEM grid (64 points, well within Open-Meteo's 100-point limit)
            var lats = new List<double>(TerrainGridSize * TerrainGridSize);
            var lons = new List<double>(TerrainGridSize * TerrainGridSize);

            var latSpan = Math.Max(0.0005, maxLat - minLat);
            var lonSpan = Math.Max(0.0005, maxLon - minLon);

            for (var r = 0; r < TerrainGridSize; r++)
            {
                var rowLat = minLat + (latSpan * r / (TerrainGridSize - 1));
                for (var c = 0; c < TerrainGridSize; c++)
                {
                    var colLon = minLon + (lonSpan * c / (TerrainGridSize - 1));
                    lats.Add(rowLat);
                    lons.Add(colLon);
                }
            }

            var latStr = string.Join(",", lats.Select(v => v.ToString("F5", CultureInfo.InvariantCulture)));
            var lonStr = string.Join(",", lons.Select(v => v.ToString("F5", CultureInfo.InvariantCulture)));
            var url = $"https://api.open-meteo.com/v1/elevation?latitude={latStr}&longitude={lonStr}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            using var response = await httpClient.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var elevArray = doc.RootElement.GetProperty("elevation");
            var elevations = new List<double>(TerrainGridSize * TerrainGridSize);
            foreach (var el in elevArray.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number)
                    elevations.Add(Math.Round(el.GetDouble(), 1));
            }

            if (elevations.Count < TerrainGridSize * TerrainGridSize)
                return BuildFallback();

            var meanElev = Math.Round(elevations.Average(), 0);
            var minElev = Math.Round(elevations.Min(), 0);
            var maxElev = Math.Round(elevations.Max(), 0);

            var spanLatM = Math.Abs(maxLat - minLat) * 111_000.0;
            var spanLonM = Math.Abs(maxLon - minLon) * 111_000.0 * Math.Cos(lat * Math.PI / 180.0);
            var diagMeters = Math.Max(100.0, Math.Sqrt(spanLatM * spanLatM + spanLonM * spanLonM));

            var slopePct = Math.Round(Math.Clamp(((maxElev - minElev) / diagMeters) * 100.0, 0.5, 45.0), 1);
            var erosionRisk = slopePct >= 12.0 ? "Alto" : slopePct >= 5.0 ? "Moderado" : "Bajo";

            var note = slopePct >= 12.0
                ? $"Terreno en ladera ({slopePct}% pendiente) a {meanElev} msnm: conviene priorizar cultivos de cobertura y raíces profundas para frenar erosión."
                : slopePct >= 5.0
                    ? $"Pendiente moderada ({slopePct}%) a {meanElev} msnm: rotar con cultivos de buena cobertura protege el suelo en lluvias."
                    : $"Terreno mayormente plano ({slopePct}% pendiente) a {meanElev} msnm: bajo riesgo de erosión por escorrentía.";

            var result = new ParcelTerrainDto
            {
                Source = "SRTM / Copernicus DEM",
                IsLiveApi = true,
                MeanElevationM = meanElev,
                MinElevationM = minElev,
                MaxElevationM = maxElev,
                EstimatedSlopePct = slopePct,
                ErosionRisk = erosionRisk,
                TerrainNote = note,
                GridSize = TerrainGridSize,
                ElevationGrid = elevations
            };

            cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al consultar elevación para ({Lat}, {Lon}).", lat, lon);
            return BuildFallback();
        }
    }

    private static ParcelTerrainDto BuildFallback()
    {
        var grid = new List<double>(TerrainGridSize * TerrainGridSize);
        for (var r = 0; r < TerrainGridSize; r++)
        {
            for (var c = 0; c < TerrainGridSize; c++)
            {
                grid.Add(Math.Round(820 + (r * 5.2) + (c * 3.8), 1));
            }
        }

        return new ParcelTerrainDto
        {
            Source = "Estimación Topográfica Regional",
            IsLiveApi = false,
            MeanElevationM = 850,
            MinElevationM = 820,
            MaxElevationM = 885,
            EstimatedSlopePct = 7.5,
            ErosionRisk = "Moderado",
            TerrainNote = "Pendiente moderada estimada (~7.5%): se recomienda incluir cultivos de cobertura en el ciclo seco.",
            GridSize = TerrainGridSize,
            ElevationGrid = grid
        };
    }
}
