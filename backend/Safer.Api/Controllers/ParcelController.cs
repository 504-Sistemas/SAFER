using Microsoft.AspNetCore.Mvc;
using Safer.Api.Models;
using Safer.Api.Services.Geospatial;

namespace Safer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParcelController(
    IGeospatialParserService parserService,
    IWebHostEnvironment env,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("parse-kml")]
    public async Task<ActionResult<ParsedParcelDto>> ParseKmlFile(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Debe adjuntar un archivo .kml válido.");

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        var result = parserService.ParseKml(content, Path.GetFileNameWithoutExtension(file.FileName));
        return Ok(result);
    }

    [HttpPost("parse-kml-raw")]
    public ActionResult<ParsedParcelDto> ParseKmlRaw([FromBody] ParseRawGeometryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("El contenido KML es requerido.");

        return Ok(parserService.ParseKml(request.Content, request.Name));
    }

    [HttpPost("parse-geojson")]
    public ActionResult<ParsedParcelDto> ParseGeoJson([FromBody] ParseRawGeometryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("El contenido GeoJSON es requerido.");

        return Ok(parserService.ParseGeoJson(request.Content, request.Name));
    }

    [HttpGet("samples")]
    public ActionResult<IEnumerable<string>> ListSampleParcels()
    {
        var dir = ResolveFeedbackDir();
        if (dir is null || !Directory.Exists(dir))
            return Ok(Array.Empty<string>());

        var files = Directory.GetFiles(dir, "*.kml")
            .Select(Path.GetFileName)
            .Where(n => n is not null)!
            .ToArray();

        return Ok(files);
    }

    [HttpGet("samples/{fileName}")]
    public async Task<ActionResult<ParsedParcelDto>> LoadSampleParcel(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        var dir = ResolveFeedbackDir();
        if (dir is null)
            return NotFound("Carpeta de muestras no encontrada.");

        var fullPath = Path.Combine(dir, safeName);
        if (!System.IO.File.Exists(fullPath))
            return NotFound($"No se encontró la parcela de muestra '{safeName}'.");

        var content = await System.IO.File.ReadAllTextAsync(fullPath);
        return Ok(parserService.ParseKml(content, Path.GetFileNameWithoutExtension(safeName)));
    }

    private string? ResolveFeedbackDir()
    {
        var configured = configuration["SampleParcelsPath"];
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        // ContentRoot is typically .../SAFER/backend/Safer.Api
        var candidate = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "feadback"));
        return Directory.Exists(candidate) ? candidate : null;
    }
}
