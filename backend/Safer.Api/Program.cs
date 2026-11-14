using Safer.Api.Services.DecisionEngine;
using Safer.Api.Services.Geospatial;
using Safer.Api.Services.Nasa;
using Safer.Api.Services.Soil;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

var allowedOrigins = new List<string> { "http://localhost:4200", "https://localhost:4200" };
var frontendUrl = builder.Configuration["FRONTEND_URL"];
if (!string.IsNullOrWhiteSpace(frontendUrl))
{
    allowedOrigins.AddRange(
        frontendUrl.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(u => u.TrimEnd('/')));
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
    {
        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins.ToArray())
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

builder.Services.AddSingleton<IGeospatialParserService, GeospatialParserService>();
builder.Services.AddSingleton<ICropCatalogService, CropCatalogService>();
builder.Services.AddSingleton<IRotationEngineService, RotationEngineService>();
builder.Services.AddSingleton<IPestRiskService, PestRiskService>();

builder.Services.AddHttpClient<INasaPowerService, NasaPowerService>();
builder.Services.AddHttpClient<INasaEonetService, NasaEonetService>();
builder.Services.AddHttpClient<IElevationService, ElevationService>();
builder.Services.AddHttpClient<INasaEarthdataService, NasaEarthdataService>();
builder.Services.AddHttpClient<ISoilService, SoilService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}

app.UseCors("AngularDev");
app.UseAuthorization();
app.MapControllers();

app.Run();
