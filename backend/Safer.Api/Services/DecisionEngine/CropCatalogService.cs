using Safer.Api.Models;

namespace Safer.Api.Services.DecisionEngine;

public interface ICropCatalogService
{
    IReadOnlyList<CropDefinition> GetAll();
    CropDefinition GetByIdOrDefault(string id);
}

public class CropCatalogService : ICropCatalogService
{
    private static readonly List<CropDefinition> Crops =
    [
        new()
        {
            Id = "maize",
            Name = "Maíz",
            ScientificName = "Zea mays",
            BotanicalFamily = "Poaceae",
            Category = "Cereal",
            CycleDays = 120,
            WaterDemandMm = 550,
            KcCoefficient = 1.15,
            NitrogenBalance = -2.0,
            OrganicMatterContribution = 4.5,
            RootDepth = "Medium",
            MinTempC = 14,
            MaxTempC = 34,
            MinPh = 5.5,
            MaxPh = 7.5,
            DroughtTolerance = 5.0,
            HeatTolerance = 6.0,
            MarketValueIndex = 7.5,
            AgronomicRole = "Cultivo base de alto aporte energético y rastrojo; alto demandante de nitrógeno."
        },
        new()
        {
            Id = "common_bean",
            Name = "Frijol Común",
            ScientificName = "Phaseolus vulgaris",
            BotanicalFamily = "Fabaceae",
            Category = "Legume",
            CycleDays = 85,
            WaterDemandMm = 340,
            KcCoefficient = 0.90,
            NitrogenBalance = 2.2,
            OrganicMatterContribution = 5.5,
            RootDepth = "Shallow",
            MinTempC = 15,
            MaxTempC = 30,
            MinPh = 5.5,
            MaxPh = 7.2,
            DroughtTolerance = 6.0,
            HeatTolerance = 5.5,
            MarketValueIndex = 8.2,
            AgronomicRole = "Leguminosa fijadora de nitrógeno de ciclo corto; ideal en postrera para recuperar fertilidad."
        },
        new()
        {
            Id = "sorghum",
            Name = "Sorgo",
            ScientificName = "Sorghum bicolor",
            BotanicalFamily = "Poaceae",
            Category = "Cereal",
            CycleDays = 110,
            WaterDemandMm = 380,
            KcCoefficient = 0.85,
            NitrogenBalance = -1.3,
            OrganicMatterContribution = 6.0,
            RootDepth = "Deep",
            MinTempC = 16,
            MaxTempC = 38,
            MinPh = 5.2,
            MaxPh = 8.0,
            DroughtTolerance = 9.2,
            HeatTolerance = 9.3,
            MarketValueIndex = 6.4,
            AgronomicRole = "Cereal altamente resiliente a sequía y calor extremo; sistema radicular profundo."
        },
        new()
        {
            Id = "soybean",
            Name = "Soya",
            ScientificName = "Glycine max",
            BotanicalFamily = "Fabaceae",
            Category = "Legume",
            CycleDays = 115,
            WaterDemandMm = 470,
            KcCoefficient = 1.05,
            NitrogenBalance = 2.5,
            OrganicMatterContribution = 6.2,
            RootDepth = "Medium",
            MinTempC = 16,
            MaxTempC = 33,
            MinPh = 5.8,
            MaxPh = 7.3,
            DroughtTolerance = 5.8,
            HeatTolerance = 6.5,
            MarketValueIndex = 8.4,
            AgronomicRole = "Fuerte fijación simbiótica de nitrógeno y alto valor comercial."
        },
        new()
        {
            Id = "crotalaria",
            Name = "Crotalaria (Abono Verde)",
            ScientificName = "Crotalaria juncea",
            BotanicalFamily = "Fabaceae",
            Category = "CoverCrop",
            CycleDays = 75,
            WaterDemandMm = 260,
            KcCoefficient = 0.70,
            NitrogenBalance = 3.0,
            OrganicMatterContribution = 9.5,
            RootDepth = "Deep",
            MinTempC = 17,
            MaxTempC = 37,
            MinPh = 5.0,
            MaxPh = 7.8,
            DroughtTolerance = 8.8,
            HeatTolerance = 9.0,
            MarketValueIndex = 3.0,
            AgronomicRole = "Cultivo de cobertura que aporta biomasa masiva y fija nitrógeno."
        },
        new()
        {
            Id = "oats_cover",
            Name = "Avena Forrajera / Cobertura",
            ScientificName = "Avena sativa",
            BotanicalFamily = "Poaceae",
            Category = "CoverCrop",
            CycleDays = 80,
            WaterDemandMm = 290,
            KcCoefficient = 0.75,
            NitrogenBalance = -0.3,
            OrganicMatterContribution = 8.4,
            RootDepth = "Shallow",
            MinTempC = 10,
            MaxTempC = 28,
            MinPh = 5.3,
            MaxPh = 7.5,
            DroughtTolerance = 6.5,
            HeatTolerance = 4.5,
            MarketValueIndex = 4.5,
            AgronomicRole = "Protege el suelo contra erosión hídrica, retiene humedad y aporta cobertura orgánica."
        },
        new()
        {
            Id = "cassava",
            Name = "Yuca",
            ScientificName = "Manihot esculenta",
            BotanicalFamily = "Euphorbiaceae",
            Category = "Tuber",
            CycleDays = 210,
            WaterDemandMm = 450,
            KcCoefficient = 0.80,
            NitrogenBalance = -1.2,
            OrganicMatterContribution = 4.0,
            RootDepth = "Deep",
            MinTempC = 18,
            MaxTempC = 37,
            MinPh = 5.0,
            MaxPh = 7.5,
            DroughtTolerance = 9.0,
            HeatTolerance = 8.8,
            MarketValueIndex = 7.6,
            AgronomicRole = "Tubérculo rústico de alta tolerancia a sequía y ruptura total de plagas de cereales."
        },
        new()
        {
            Id = "sweet_potato",
            Name = "Camote",
            ScientificName = "Ipomoea batatas",
            BotanicalFamily = "Convolvulaceae",
            Category = "Tuber",
            CycleDays = 120,
            WaterDemandMm = 370,
            KcCoefficient = 0.85,
            NitrogenBalance = -0.8,
            OrganicMatterContribution = 6.5,
            RootDepth = "Medium",
            MinTempC = 16,
            MaxTempC = 35,
            MinPh = 5.2,
            MaxPh = 7.2,
            DroughtTolerance = 7.8,
            HeatTolerance = 8.0,
            MarketValueIndex = 7.8,
            AgronomicRole = "Cobertura rápida de suelo que reduce evaporación y diversifica ingresos."
        },
        new()
        {
            Id = "sunflower",
            Name = "Girasol",
            ScientificName = "Helianthus annuus",
            BotanicalFamily = "Asteraceae",
            Category = "CashCrop",
            CycleDays = 105,
            WaterDemandMm = 410,
            KcCoefficient = 0.95,
            NitrogenBalance = -1.1,
            OrganicMatterContribution = 5.2,
            RootDepth = "Deep",
            MinTempC = 13,
            MaxTempC = 35,
            MinPh = 5.7,
            MaxPh = 7.8,
            DroughtTolerance = 8.2,
            HeatTolerance = 8.3,
            MarketValueIndex = 7.9,
            AgronomicRole = "Raíz pivotante profunda que descompacta el suelo y aprovecha humedad residual."
        }
    ];

    public IReadOnlyList<CropDefinition> GetAll() => Crops;

    public CropDefinition GetByIdOrDefault(string id) =>
        Crops.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Crops[0];
}
