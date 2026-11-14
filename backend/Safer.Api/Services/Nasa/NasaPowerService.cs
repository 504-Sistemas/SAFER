using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Safer.Api.Models;

namespace Safer.Api.Services.Nasa;

public interface INasaPowerService
{
    Task<NasaPowerClimateDto> GetClimatologyAsync(double latitude, double longitude, CancellationToken ct = default);
    Task<AnnualMonthlyProfileDto> GetAnnualMonthlyProfileAsync(double latitude, double longitude, int year, CancellationToken ct = default);
    Task<MonthlyDailyPrecipitationDto> GetDailyPrecipitationForMonthAsync(double latitude, double longitude, int year, int month, CancellationToken ct = default);
    Task<RecentWeatherAnomalyDto> GetRecent30DayAnomalyAsync(double latitude, double longitude, NasaPowerClimateDto climatology, CancellationToken ct = default);
}

public class NasaPowerService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<NasaPowerService> logger) : INasaPowerService
{
    private static readonly string[] MonthKeys =
        ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

    private static readonly string[] MonthLabels =
        ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];

    private static readonly int[] DaysInMonth = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

    public async Task<NasaPowerClimateDto> GetClimatologyAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var cacheKey = $"nasa_power_{Math.Round(latitude, 2)}_{Math.Round(longitude, 2)}";
        if (cache.TryGetValue(cacheKey, out NasaPowerClimateDto? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://power.larc.nasa.gov/api/temporal/climatology/point?parameters=PRECTOTCORR,T2M,T2M_MAX,T2M_MIN,ALLSKY_SFC_SW_DWN,RH2M&community=AG&longitude={0:F4}&latitude={1:F4}&format=JSON",
                longitude,
                latitude);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await httpClient.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var parameter = doc.RootElement
                .GetProperty("properties")
                .GetProperty("parameter");

            var precipObj = parameter.GetProperty("PRECTOTCORR");
            var t2mObj = parameter.GetProperty("T2M");
            var tMaxObj = parameter.GetProperty("T2M_MAX");
            var tMinObj = parameter.GetProperty("T2M_MIN");
            var solarObj = parameter.GetProperty("ALLSKY_SFC_SW_DWN");
            var rhObj = parameter.GetProperty("RH2M");

            var monthly = new List<MonthlyClimatePoint>(12);
            for (var i = 0; i < 12; i++)
            {
                var key = MonthKeys[i];
                // NASA POWER PRECTOTCORR climatology is in mm/day; multiply by days in month for monthly mm
                var precipMmDay = ReadVal(precipObj, key, 3.0);
                var monthlyPrecipMm = Math.Round(precipMmDay * DaysInMonth[i], 1);
                var tMean = Math.Round(ReadVal(t2mObj, key, 22.0), 1);
                var solar = Math.Round(ReadVal(solarObj, key, 5.0), 2);
                var et0Mm = CalculateMonthlyEt0Mm(tMean, solar, DaysInMonth[i]);

                monthly.Add(new MonthlyClimatePoint
                {
                    Month = i + 1,
                    MonthName = MonthLabels[i],
                    PrecipitationMm = monthlyPrecipMm,
                    TempMeanC = tMean,
                    TempMaxC = Math.Round(ReadVal(tMaxObj, key, 28.0), 1),
                    TempMinC = Math.Round(ReadVal(tMinObj, key, 16.0), 1),
                    SolarRadiationKwhM2Day = solar,
                    RelativeHumidityPct = Math.Round(ReadVal(rhObj, key, 72.0), 1),
                    ReferenceEt0Mm = et0Mm,
                    WaterBalanceMm = Math.Round(monthlyPrecipMm - et0Mm, 1)
                });
            }

            var result = BuildSummary(latitude, longitude, monthly, isLiveApi: true, "NASA POWER Climatology API (Live)");
            cache.Set(cacheKey, result, TimeSpan.FromHours(6));
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al consultar NASA POWER API para ({Lat}, {Lon}). Usando perfil regional fallback.", latitude, longitude);
            return BuildFallbackClimatology(latitude, longitude);
        }
    }

    private static double ReadVal(JsonElement obj, string key, double fallback)
    {
        if (obj.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.Number)
        {
            var d = val.GetDouble();
            return d <= -900 ? fallback : d; // NASA POWER uses -999 for missing values
        }
        return fallback;
    }

    private static NasaPowerClimateDto BuildSummary(
        double latitude,
        double longitude,
        List<MonthlyClimatePoint> monthly,
        bool isLiveApi,
        string source)
    {
        var annualPrecip = Math.Round(monthly.Sum(m => m.PrecipitationMm), 1);
        var annualEt0 = Math.Round(monthly.Sum(m => m.ReferenceEt0Mm), 1);
        var annualBalance = Math.Round(annualPrecip - annualEt0, 1);
        var meanTemp = Math.Round(monthly.Average(m => m.TempMeanC), 1);
        var maxTemp = Math.Round(monthly.Max(m => m.TempMaxC), 1);
        var minTemp = Math.Round(monthly.Min(m => m.TempMinC), 1);
        var meanSolar = Math.Round(monthly.Average(m => m.SolarRadiationKwhM2Day), 2);
        var meanRh = Math.Round(monthly.Average(m => m.RelativeHumidityPct), 1);
        var dryMonths = monthly.Count(m => m.PrecipitationMm < 60.0);

        var waterRisk = annualPrecip < 850 || dryMonths >= 6 ? "Riesgo Alto"
            : annualPrecip < 1300 || dryMonths >= 4 ? "Riesgo Medio"
            : "Riesgo Bajo";

        var thermalRisk = maxTemp >= 34.0 ? "Alto"
            : maxTemp >= 29.5 ? "Moderado"
            : "Bajo";

        return new NasaPowerClimateDto
        {
            Source = source,
            IsLiveApi = isLiveApi,
            Latitude = Math.Round(latitude, 5),
            Longitude = Math.Round(longitude, 5),
            AnnualPrecipitationMm = annualPrecip,
            AnnualEt0Mm = annualEt0,
            AnnualWaterBalanceMm = annualBalance,
            MeanTemperatureC = meanTemp,
            MaxTemperatureC = maxTemp,
            MinTemperatureC = minTemp,
            MeanSolarRadiationKwhM2Day = meanSolar,
            MeanRelativeHumidityPct = meanRh,
            DryMonthsCount = dryMonths,
            WaterDeficitRisk = waterRisk,
            ThermalStressRisk = thermalRisk,
            MonthlyProfile = monthly
        };
    }

    private static NasaPowerClimateDto BuildFallbackClimatology(double latitude, double longitude)
    {
        // Tropical/Central American seasonal baseline (dry Nov-Apr, wet May-Oct)
        double[] precip = [18, 15, 22, 55, 175, 230, 185, 210, 265, 195, 58, 25];
        double[] tMean = [20.5, 21.4, 22.8, 23.9, 23.6, 22.4, 22.1, 22.3, 22.0, 21.6, 20.9, 20.3];

        var monthly = Enumerable.Range(0, 12).Select(i =>
        {
            var solar = i < 4 ? 5.7 : 4.8;
            var et0Mm = CalculateMonthlyEt0Mm(tMean[i], solar, DaysInMonth[i]);
            return new MonthlyClimatePoint
            {
                Month = i + 1,
                MonthName = MonthLabels[i],
                PrecipitationMm = precip[i],
                TempMeanC = tMean[i],
                TempMaxC = Math.Round(tMean[i] + 5.8, 1),
                TempMinC = Math.Round(tMean[i] - 5.2, 1),
                SolarRadiationKwhM2Day = solar,
                RelativeHumidityPct = precip[i] > 100 ? 79.0 : 64.0,
                ReferenceEt0Mm = et0Mm,
                WaterBalanceMm = Math.Round(precip[i] - et0Mm, 1)
            };
        }).ToList();

        return BuildSummary(latitude, longitude, monthly, isLiveApi: false, "NASA POWER Regional Fallback (Offline/Timeout)");
    }

    /// <summary>
    /// Computes reference evapotranspiration (ET0) in mm/month using the Priestley-Taylor formulation
    /// driven by NASA POWER solar radiation (ALLSKY_SFC_SW_DWN in MJ/m2/day) and air temperature.
    /// </summary>
    private static double CalculateMonthlyEt0Mm(double tempMeanC, double solarMjM2Day, int daysInMonth)
    {
        var rsMjM2Day = Math.Clamp(solarMjM2Day, 2.0, 32.0);
        var rnMjM2Day = 0.55 * rsMjM2Day;

        var es = 0.6108 * Math.Exp((17.27 * tempMeanC) / (tempMeanC + 237.3));
        var delta = (4098.0 * es) / Math.Pow(tempMeanC + 237.3, 2);
        const double gamma = 0.066;
        const double lambda = 2.45;

        var et0MmDay = 1.26 * (delta / (delta + gamma)) * (rnMjM2Day / lambda);
        return Math.Round(Math.Max(15.0, et0MmDay * daysInMonth), 1);
    }

    public async Task<AnnualMonthlyProfileDto> GetAnnualMonthlyProfileAsync(
        double latitude,
        double longitude,
        int year,
        CancellationToken ct = default)
    {
        var (daysByMonth, isLive, source) = await GetOrFetchYearDailyAsync(latitude, longitude, year, ct);

        var monthly = Enumerable.Range(1, 12).Select(m =>
        {
            var days = daysByMonth.TryGetValue(m, out var list) ? list : [];
            var totalPrecip = Math.Round(days.Sum(d => d.PrecipitationMm), 1);
            var meanTemp = days.Count > 0 ? Math.Round(days.Average(d => d.TempMeanC), 1) : 22.0;

            return new MonthlyClimatePoint
            {
                Month = m,
                MonthName = MonthLabels[m - 1],
                PrecipitationMm = totalPrecip,
                TempMeanC = meanTemp,
                TempMaxC = Math.Round(meanTemp + 5.0, 1),
                TempMinC = Math.Round(meanTemp - 5.0, 1),
                SolarRadiationKwhM2Day = 5.0,
                RelativeHumidityPct = 72.0
            };
        }).ToList();

        return new AnnualMonthlyProfileDto
        {
            Year = year,
            IsLiveApi = isLive,
            Source = source,
            MonthlyProfile = monthly
        };
    }

    public async Task<MonthlyDailyPrecipitationDto> GetDailyPrecipitationForMonthAsync(
        double latitude,
        double longitude,
        int year,
        int month,
        CancellationToken ct = default)
    {
        var clampedMonth = Math.Clamp(month, 1, 12);
        var (daysByMonth, isLive, source) = await GetOrFetchYearDailyAsync(latitude, longitude, year, ct);
        var days = daysByMonth.TryGetValue(clampedMonth, out var list) ? list : [];

        return new MonthlyDailyPrecipitationDto
        {
            Year = year,
            Month = clampedMonth,
            MonthName = MonthLabels[clampedMonth - 1],
            IsLiveApi = isLive,
            Source = source,
            TotalPrecipitationMm = Math.Round(days.Sum(d => d.PrecipitationMm), 1),
            RainyDaysCount = days.Count(d => d.PrecipitationMm >= 0.5),
            Days = days
        };
    }

    private async Task<(Dictionary<int, List<DailyPrecipitationPoint>> DaysByMonth, bool IsLive, string Source)> GetOrFetchYearDailyAsync(
        double latitude,
        double longitude,
        int year,
        CancellationToken ct)
    {
        var clampedYear = Math.Clamp(year, 1981, 2025);
        var cacheKey = $"nasa_power_daily_{Math.Round(latitude, 2)}_{Math.Round(longitude, 2)}_{clampedYear}";
        if (cache.TryGetValue(cacheKey, out Dictionary<int, List<DailyPrecipitationPoint>>? cached) && cached is not null)
        {
            return (cached, true, "NASA POWER Daily API (Live/Cached)");
        }

        try
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://power.larc.nasa.gov/api/temporal/daily/point?parameters=PRECTOTCORR,T2M&community=AG&longitude={0:F4}&latitude={1:F4}&start={2}0101&end={2}1231&format=JSON",
                longitude,
                latitude,
                clampedYear);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(12));

            using var response = await httpClient.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var parameter = doc.RootElement.GetProperty("properties").GetProperty("parameter");
            var precipObj = parameter.GetProperty("PRECTOTCORR");
            var tempObj = parameter.GetProperty("T2M");

            var byMonth = Enumerable.Range(1, 12).ToDictionary(m => m, _ => new List<DailyPrecipitationPoint>());

            foreach (var prop in precipObj.EnumerateObject())
            {
                if (prop.Name.Length != 8) continue;
                if (!int.TryParse(prop.Name.AsSpan(4, 2), out var m) || m < 1 || m > 12) continue;
                if (!int.TryParse(prop.Name.AsSpan(6, 2), out var d)) continue;

                var precip = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetDouble() : 0.0;
                if (precip <= -900) precip = 0.0;

                var temp = ReadVal(tempObj, prop.Name, 22.0);

                byMonth[m].Add(new DailyPrecipitationPoint
                {
                    Date = $"{prop.Name[..4]}-{prop.Name[4..6]}-{prop.Name[6..8]}",
                    DayOfMonth = d,
                    PrecipitationMm = Math.Round(precip, 2),
                    TempMeanC = Math.Round(temp, 1)
                });
            }

            cache.Set(cacheKey, byMonth, TimeSpan.FromHours(6));
            return (byMonth, true, "NASA POWER Daily API (Live)");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al consultar NASA POWER Daily API para año {Year}. Usando fallback diario.", clampedYear);
            return (BuildFallbackDailyYear(clampedYear), false, "NASA POWER Daily Fallback");
        }
    }

    private static Dictionary<int, List<DailyPrecipitationPoint>> BuildFallbackDailyYear(int year)
    {
        double[] monthlyAvg = [18, 15, 22, 55, 175, 230, 185, 210, 265, 195, 58, 25];
        var rng = new Random(year);
        var byMonth = new Dictionary<int, List<DailyPrecipitationPoint>>();

        for (var m = 1; m <= 12; m++)
        {
            var daysCount = DateTime.DaysInMonth(year, m);
            var list = new List<DailyPrecipitationPoint>(daysCount);
            var target = monthlyAvg[m - 1];
            var rainyProb = target > 100 ? 0.52 : 0.18;

            for (var d = 1; d <= daysCount; d++)
            {
                var isRain = rng.NextDouble() < rainyProb;
                var rainMm = isRain ? Math.Round((target / (daysCount * rainyProb)) * (0.4 + rng.NextDouble() * 1.2), 1) : 0.0;
                list.Add(new DailyPrecipitationPoint
                {
                    Date = $"{year:D4}-{m:D2}-{d:D2}",
                    DayOfMonth = d,
                    PrecipitationMm = rainMm,
                    TempMeanC = Math.Round(21.0 + (m >= 3 && m <= 5 ? 2.5 : 0.0), 1)
                });
            }
            byMonth[m] = list;
        }
        return byMonth;
    }

    public async Task<RecentWeatherAnomalyDto> GetRecent30DayAnomalyAsync(
        double latitude,
        double longitude,
        NasaPowerClimateDto climatology,
        CancellationToken ct = default)
    {
        var cacheKey = $"nasa_power_30d_{Math.Round(latitude, 2)}_{Math.Round(longitude, 2)}";
        if (cache.TryGetValue(cacheKey, out RecentWeatherAnomalyDto? cached) && cached is not null)
        {
            return cached;
        }

        // Compare against climatology of the current month
        var currentMonth = DateTime.UtcNow.Month;
        var normalMonth = climatology.MonthlyProfile.FirstOrDefault(m => m.Month == currentMonth);
        var normal30Days = normalMonth?.PrecipitationMm ?? 90.0;

        try
        {
            // NASA POWER daily has ~3-4 days latency
            var endDate = DateTime.UtcNow.AddDays(-4);
            var startDate = endDate.AddDays(-30);

            var url = string.Format(
                CultureInfo.InvariantCulture,
                "https://power.larc.nasa.gov/api/temporal/daily/point?parameters=PRECTOTCORR,T2M&community=AG&longitude={0:F4}&latitude={1:F4}&start={2}&end={3}&format=JSON",
                longitude,
                latitude,
                startDate.ToString("yyyyMMdd"),
                endDate.ToString("yyyyMMdd"));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            using var response = await httpClient.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            var parameter = doc.RootElement.GetProperty("properties").GetProperty("parameter");
            var precipObj = parameter.GetProperty("PRECTOTCORR");
            var tempObj = parameter.GetProperty("T2M");

            double sumRain = 0;
            var temps = new List<double>();

            foreach (var prop in precipObj.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    var v = prop.Value.GetDouble();
                    if (v > -900) sumRain += v;
                }
            }
            foreach (var prop in tempObj.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    var v = prop.Value.GetDouble();
                    if (v > -900) temps.Add(v);
                }
            }

            var rain30 = Math.Round(sumRain, 1);
            var temp30 = temps.Count > 0 ? Math.Round(temps.Average(), 1) : climatology.MeanTemperatureC;
            var anomalyPct = normal30Days > 1 ? Math.Round(((rain30 - normal30Days) / normal30Days) * 100.0, 0) : 0;

            var status = anomalyPct <= -20 ? "Más seco de lo normal"
                : anomalyPct >= 20 ? "Más húmedo de lo normal"
                : "Dentro de lo normal";

            var diffText = anomalyPct < 0
                ? $"{Math.Abs(anomalyPct):F0}% menos lluvia de lo normal"
                : anomalyPct > 0
                    ? $"{anomalyPct:F0}% más lluvia de lo normal"
                    : "lluvia acorde al promedio histórico";

            var dto = new RecentWeatherAnomalyDto
            {
                IsLiveApi = true,
                Precipitation30DaysMm = rain30,
                Normal30DaysMm = Math.Round(normal30Days, 1),
                AnomalyPct = anomalyPct,
                MeanTemp30DaysC = temp30,
                StatusLabel = status,
                SummaryText = $"En los últimos 30 días cayeron {rain30:F1} mm ({diffText} para esta época; promedio {normal30Days:F0} mm)."
            };

            cache.Set(cacheKey, dto, TimeSpan.FromHours(6));
            return dto;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo obtener anomalía de últimos 30 días en NASA POWER.");
            return new RecentWeatherAnomalyDto
            {
                IsLiveApi = false,
                Precipitation30DaysMm = Math.Round(normal30Days, 1),
                Normal30DaysMm = Math.Round(normal30Days, 1),
                AnomalyPct = 0,
                MeanTemp30DaysC = climatology.MeanTemperatureC,
                StatusLabel = "Referencia histórica",
                SummaryText = $"Referencia climática esperada para este mes: {normal30Days:F0} mm."
            };
        }
    }
}
