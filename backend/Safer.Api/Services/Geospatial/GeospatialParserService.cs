using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Safer.Api.Models;

namespace Safer.Api.Services.Geospatial;

public interface IGeospatialParserService
{
    ParsedParcelDto ParseKml(string kmlContent, string? fallbackName = null);
    ParsedParcelDto ParseGeoJson(string geoJsonContent, string? fallbackName = null);
    ParsedParcelDto BuildFromCoordinates(IReadOnlyList<GeoCoordinate> coordinates, string name, string sourceFormat);
}

public class GeospatialParserService : IGeospatialParserService
{
    private const double EarthRadiusMeters = 6_371_000.0;

    public ParsedParcelDto ParseKml(string kmlContent, string? fallbackName = null)
    {
        if (string.IsNullOrWhiteSpace(kmlContent))
            throw new ArgumentException("El contenido KML está vacío.", nameof(kmlContent));

        var doc = XDocument.Parse(kmlContent);
        var placemark = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Placemark");
        var name = placemark?.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "name")?.Value?.Trim();
        }
        name = string.IsNullOrWhiteSpace(name) ? (fallbackName ?? "Parcela KML") : name;

        var coordElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "coordinates")
            ?? throw new FormatException("No se encontró un elemento <coordinates> válido en el archivo KML.");

        var tokens = coordElement.Value
            .Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);

        var coords = new List<GeoCoordinate>();
        foreach (var token in tokens)
        {
            var parts = token.Split(',');
            if (parts.Length < 2) continue;

            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            {
                coords.Add(new GeoCoordinate(lat, lon));
            }
        }

        if (coords.Count < 3)
            throw new FormatException("El polígono KML debe contener al menos 3 coordenadas válidas.");

        return BuildFromCoordinates(coords, name, "KML");
    }

    public ParsedParcelDto ParseGeoJson(string geoJsonContent, string? fallbackName = null)
    {
        if (string.IsNullOrWhiteSpace(geoJsonContent))
            throw new ArgumentException("El contenido GeoJSON está vacío.", nameof(geoJsonContent));

        using var doc = JsonDocument.Parse(geoJsonContent);
        var root = doc.RootElement;

        string? name = fallbackName;
        JsonElement geometry = root;

        if (root.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (typeStr == "FeatureCollection" && root.TryGetProperty("features", out var features) && features.GetArrayLength() > 0)
            {
                var firstFeature = features[0];
                name ??= TryReadFeatureName(firstFeature);
                geometry = firstFeature.GetProperty("geometry");
            }
            else if (typeStr == "Feature")
            {
                name ??= TryReadFeatureName(root);
                geometry = root.GetProperty("geometry");
            }
        }

        if (!geometry.TryGetProperty("type", out var geomTypeProp))
            throw new FormatException("Geometría GeoJSON inválida.");

        var geomType = geomTypeProp.GetString();
        var coords = new List<GeoCoordinate>();

        if (geomType == "Polygon")
        {
            var ring = geometry.GetProperty("coordinates")[0];
            foreach (var pt in ring.EnumerateArray())
            {
                coords.Add(new GeoCoordinate(pt[1].GetDouble(), pt[0].GetDouble()));
            }
        }
        else if (geomType == "MultiPolygon")
        {
            var ring = geometry.GetProperty("coordinates")[0][0];
            foreach (var pt in ring.EnumerateArray())
            {
                coords.Add(new GeoCoordinate(pt[1].GetDouble(), pt[0].GetDouble()));
            }
        }
        else if (geomType == "Point")
        {
            var pt = geometry.GetProperty("coordinates");
            var lon = pt[0].GetDouble();
            var lat = pt[1].GetDouble();
            // Create a small ~1 ha square around point so bounding box and area work uniformly
            const double delta = 0.00045;
            coords.AddRange(
            [
                new GeoCoordinate(lat - delta, lon - delta),
                new GeoCoordinate(lat - delta, lon + delta),
                new GeoCoordinate(lat + delta, lon + delta),
                new GeoCoordinate(lat + delta, lon - delta),
                new GeoCoordinate(lat - delta, lon - delta)
            ]);
        }
        else
        {
            throw new FormatException($"Tipo de geometría GeoJSON no soportado: {geomType}");
        }

        if (coords.Count < 3)
            throw new FormatException("La geometría debe tener al menos 3 vértices.");

        return BuildFromCoordinates(coords, name ?? "Parcela GeoJSON", "GeoJSON");
    }

    public ParsedParcelDto BuildFromCoordinates(IReadOnlyList<GeoCoordinate> coordinates, string name, string sourceFormat)
    {
        var ring = coordinates.ToList();
        if (ring[0] != ring[^1])
        {
            ring.Add(ring[0]);
        }

        var uniqueVertices = ring.Take(ring.Count - 1).ToList();
        var minLat = uniqueVertices.Min(c => c.Latitude);
        var maxLat = uniqueVertices.Max(c => c.Latitude);
        var minLon = uniqueVertices.Min(c => c.Longitude);
        var maxLon = uniqueVertices.Max(c => c.Longitude);

        var centroidLat = uniqueVertices.Average(c => c.Latitude);
        var centroidLon = uniqueVertices.Average(c => c.Longitude);
        var areaHectares = CalculateGeodesicAreaHectares(ring, centroidLat);

        var geoJsonObj = new
        {
            type = "Feature",
            properties = new { name, areaHectares = Math.Round(areaHectares, 2) },
            geometry = new
            {
                type = "Polygon",
                coordinates = new[]
                {
                    ring.Select(c => new[] { c.Longitude, c.Latitude }).ToArray()
                }
            }
        };

        return new ParsedParcelDto
        {
            Name = name,
            SourceFormat = sourceFormat,
            Coordinates = ring,
            Centroid = new GeoCoordinate(Math.Round(centroidLat, 6), Math.Round(centroidLon, 6)),
            BoundingBox = new BoundingBox(
                Math.Round(minLon, 6),
                Math.Round(minLat, 6),
                Math.Round(maxLon, 6),
                Math.Round(maxLat, 6)),
            AreaHectares = Math.Round(areaHectares, 2),
            GeoJsonFeature = JsonSerializer.Serialize(geoJsonObj)
        };
    }

    private static string? TryReadFeatureName(JsonElement feature)
    {
        if (feature.TryGetProperty("properties", out var props) &&
            props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty("name", out var nameEl) &&
            nameEl.ValueKind == JsonValueKind.String)
        {
            return nameEl.GetString();
        }
        return null;
    }

    private static double CalculateGeodesicAreaHectares(IReadOnlyList<GeoCoordinate> ring, double refLatDeg)
    {
        var refLatRad = refLatDeg * Math.PI / 180.0;
        var metersPerDegLat = Math.PI / 180.0 * EarthRadiusMeters;
        var metersPerDegLon = metersPerDegLat * Math.Cos(refLatRad);

        double sum = 0;
        for (var i = 0; i < ring.Count - 1; i++)
        {
            var x1 = ring[i].Longitude * metersPerDegLon;
            var y1 = ring[i].Latitude * metersPerDegLat;
            var x2 = ring[i + 1].Longitude * metersPerDegLon;
            var y2 = ring[i + 1].Latitude * metersPerDegLat;
            sum += (x1 * y2) - (x2 * y1);
        }

        var areaSqMeters = Math.Abs(sum) / 2.0;
        return areaSqMeters / 10_000.0;
    }
}
