namespace Safer.Api.Models;

public class SoilProfileDto
{
    public string Source { get; set; } = "ISRIC SoilGrids / Regional Baseline";
    public bool IsLiveApi { get; set; }
    public string TextureClass { get; set; } = "Franco Arcilloso";
    public double ClayPct { get; set; } = 30.0;
    public double SandPct { get; set; } = 36.0;
    public double SiltPct { get; set; } = 34.0;
    public double Ph { get; set; } = 6.2;
    public double OrganicCarbonPct { get; set; } = 1.8;
    public string NitrogenLevel { get; set; } = "Medium"; // Low, Medium, High
    public string Drainage { get; set; } = "Moderate";    // Poor, Moderate, Good
}

public class SoilOverrideDto
{
    public string? TextureClass { get; set; }
    public double? ClayPct { get; set; }
    public double? SandPct { get; set; }
    public double? SiltPct { get; set; }
    public double? Ph { get; set; }
    public double? OrganicCarbonPct { get; set; }
    public string? NitrogenLevel { get; set; }
    public string? Drainage { get; set; }
}
