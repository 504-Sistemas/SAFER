namespace Safer.Api.Models;

public record GeoCoordinate(double Latitude, double Longitude);

public record BoundingBox(double MinLon, double MinLat, double MaxLon, double MaxLat);

public class ParsedParcelDto
{
    public string Name { get; set; } = "Parcela";
    public string SourceFormat { get; set; } = "GeoJSON";
    public List<GeoCoordinate> Coordinates { get; set; } = [];
    public GeoCoordinate Centroid { get; set; } = new(0, 0);
    public BoundingBox BoundingBox { get; set; } = new(0, 0, 0, 0);
    public double AreaHectares { get; set; }
    public string GeoJsonFeature { get; set; } = string.Empty;
}

public class ParseRawGeometryRequest
{
    public string Content { get; set; } = string.Empty;
    public string? Name { get; set; }
}
