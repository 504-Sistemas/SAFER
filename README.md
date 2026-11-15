# SAFER — Climate-Resilient Crop Rotation Decision-Support Tool

**SAFER** is a geospatial web application built for the **NASA Space Apps Challenge** to help farmers and agricultural technicians design **climate-resilient crop rotation strategies** that strengthen soil health, conserve water, and stabilize yields under changing temperatures, irregular rainfall, and extreme weather events.

By combining **NASA Earth observations**, **local soil properties**, **agronomic crop characteristics**, and **farmer-defined priorities**, SAFER transforms complex satellite and climate datasets into an actionable 3-cycle annual rotation plan—complete with a **3D Farm Digital Twin** and a **What-If Climate Stress Simulator**.

---

## Key Features

### 1. Geospatial Parcel Input & 3D Farm Digital Twin
- **Multiple input modes:** Upload `.kml` or GeoJSON files, load built-in sample farms (`feadback/Finca1.kml` – `finca5.kml`), or draw field boundaries directly on the interactive satellite map.
- **Automatic geometry metrics:** Computes centroid, bounding box, and geodesic area in hectares (`GeospatialParserService`).
- **Interactive 3D Farm Digital Twin:** Renders a 6×6 terrain elevation grid (`ElevationService`) with **Three.js** (`farm-twin-3d.component.ts`), allowing users to inspect topography and parcel layout in 3D with expand/full-view support.

### 2. NASA Agroclimatic & Satellite Diagnostics
- **Climate KPIs & Monthly/Daily Rainfall:** Annual precipitation, dry months, mean/max temperatures, water-deficit risk, and heat-stress indicators powered by **NASA POWER** (both climatology and daily time-series).
- **Satellite Imagery Discovery:** Queries **NASA CMR STAC (`HLSS30.v2.0`)** for recent Harmonized Landsat Sentinel-2 scenes over the parcel bounding box, showing acquisition dates and cloud cover.
- **Live Map Overlays:** Toggle **NASA GIBS WMTS** raster layers (*MODIS Terra 8-Day NDVI* and *GPM IMERG Precipitation Rate*) directly over the field polygon in **Leaflet**.
- **Regional Natural Event Monitoring:** Queries **NASA EONET** for active natural events within ~65 km of the parcel (`NasaEonetService`).

### 3. Local Soil Profile & Farmer Priorities
- **Automated Soil Baseline + Manual Override:** Fetches clay, sand, silt, pH (`phh2o`), and soil organic carbon (`soc`) from **ISRIC SoilGrids v2.0** (`0–5 cm`), with an optional manual adjustment mode for farmers who have lab soil tests.
- **5 Weighted Farmer Priorities (0–100%):**
  1. Water Conservation
  2. Soil Health & Organic Matter
  3. Natural Pest Control
  4. Economic Stability
  5. Climate Resilience

### 4. Multi-Criteria Crop Rotation Engine (Top 3) & Pest Risk
- **3-Cycle Annual Planning:** Evaluates 3-season sequences (*Cycle 1 → Cycle 2 → Cycle 3 / Cover*) across a 9-crop agronomic catalog (`CropCatalogService`: Maize, Common Bean, Sorghum, Soybean, Sunn Hemp / *Crotalaria*, Oats, Cassava, Sweet Potato, Sunflower).
- **Agronomic Guardrails:** Prohibits consecutive crops from the same botanical family to break pest cycles, checks thermal/pH/water-availability thresholds, and rewards nitrogen-fixing legumes and organic-matter-building cover crops.
- **Pest & Disease Risk Assessment:** Evaluates fungal disease and insect pest pressure using NASA POWER relative humidity, precipitation, and temperature profiles (`PestRiskService`).

### 5. "What-If" Climate Stress Simulator
Compares the farmer's **current monoculture baseline** against the **Top #1 Recommended Rotation** under three climate scenarios:
1. **Normal Climate Year** (NASA POWER baseline)
2. **Severe Drought** (`-25%` precipitation)
3. **Heat Wave** (`+2.0°C` temperature increase)

Displays projected **Yield Stability (%)**, **Soil Health Index**, and **Estimated Water Savings (%)**.

---

## NASA & Earth Observation Data Sources

| Source / API | Usage in SAFER | Endpoint / Dataset |
| :--- | :--- | :--- |
| **NASA POWER API** | Climatology & daily precipitation (`PRECTOTCORR`), temperatures (`T2M`, `T2M_MAX`, `T2M_MIN`), solar radiation (`ALLSKY_SFC_SW_DWN`), relative humidity (`RH2M`). | `https://power.larc.nasa.gov/api/temporal/climatology/point` |
| **NASA CMR STAC** | Harmonized Landsat Sentinel-2 (`HLSS30.v2.0`) scene search over parcel bounding box. | `https://cmr.earthdata.nasa.gov/stac/LPCLOUD/search` |
| **NASA GIBS (WMTS)** | Live satellite raster overlays (`MODIS_Terra_NDVI_8Day`, `IMERG_Precipitation_Rate`). | `https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/...` |
| **NASA EONET** | Regional natural hazard & event tracking near the farm. | `NasaEonetService.cs` |
| **ISRIC SoilGrids v2.0** | Local soil texture, pH, and organic carbon baseline (`0–5 cm`). | `https://rest.isric.org/soilgrids/v2.0/properties/query` |

---

## Tech Stack

- **Backend:** C# / ASP.NET Core (`.NET 10.0`), `Microsoft.AspNetCore.OpenApi`, `IMemoryCache`
- **Frontend:** TypeScript `5.7`, Angular `19.2`, Leaflet `1.9`, Three.js `0.187`, RxJS `7.8`, SCSS
- **Testing:** `Safer.Tests` (Backend unit tests for geospatial parsing & rotation engine)
- **Containerization:** Multi-stage `Dockerfile` (`.NET 10 SDK` → `.NET 10 ASP.NET Runtime`)

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js 20+](https://nodejs.org/) & npm
- (Optional) [Docker](https://www.docker.com/)

### Run Locally

**1. Start the Backend (`Safer.Api`)**
```bash
cd backend/Safer.Api
dotnet restore
dotnet run
```

**2. Start the Frontend (`safer-web`)**
```bash
cd frontend
npm install
npm start
```
Open `http://localhost:4200` in your browser (configured with `proxy.conf.json` to forward `/api` requests to the backend).

**3. Run Backend Tests**
```bash
cd backend
dotnet test
```

### Run Backend with Docker
```bash
docker build -t safer-api .
docker run -p 10000:10000 safer-api
```

---

## Repository Structure

```text
SAFER/
├── backend/
│   ├── Safer.Api/
│   │   ├── Controllers/          # EarthData, Parcel, Rotation REST endpoints
│   │   ├── Models/               # Geospatial, NASA, Soil, and Decision DTOs
│   │   └── Services/
│   │       ├── DecisionEngine/   # CropCatalog, PestRisk, RotationEngine (MCDA)
│   │       ├── Geospatial/       # KML/GeoJSON parser, Elevation grid
│   │       ├── Nasa/             # NASA POWER, CMR STAC, EONET clients
│   │       └── Soil/             # ISRIC SoilGrids v2.0 client
│   └── Safer.Tests/              # Unit tests
├── frontend/                     # Angular 19 + Leaflet + Three.js 3D Farm Twin
├── feadback/                     # Sample KML farms, pitch scripts, demo videos, docs
└── Dockerfile
```

---

## Team & License

- **Team:** SAFER
- **License:** [MIT License](LICENSE)
