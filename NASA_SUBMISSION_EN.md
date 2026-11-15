# NASA Space Apps Challenge — Project Submission Guide (English Copy-Paste Ready)

Use the sections below to fill out the official submission form on [spaceappschallenge.org](https://www.spaceappschallenge.org/). All text is written in English as required by the platform.

---

## 1. Basic Project Information

- **Project Name:**
  `SAFER — Climate-Resilient Crop Rotation Decision-Support Tool`

- **Team Name:**
  `SAFER`

- **Challenge:**
  *(Select the official Climate-Resilient Crop Rotation / Agriculture challenge on the platform)*

- **Link to Code Repository (Public):**
  `https://github.com/504-Sistemas/SAFER`

---

## 2. High-Level Project Summary (Tagline / Short Summary)

> **Copy & Paste:**
> SAFER is an interactive geospatial decision-support web application that combines NASA Earth observations (POWER, CMR STAC HLS, GIBS, and EONET), local soil data (ISRIC SoilGrids), agronomic crop profiles, and farmer priorities to generate climate-resilient 3-cycle crop rotation plans, 3D terrain twins, and "What-If" drought and heat-wave simulations.

---

## 3. Project Demo (30-Second Video)

- **File in repository:** `feadback/SAFER_Pitch_30s_EN.mp4`
- **Verified metadata:** Duration `00:00:30` (meets the strict 30-second limit), Size `~1.01 MB`.
- **Action required before submitting:**
  1. Upload `feadback/SAFER_Pitch_30s_EN.mp4` to **YouTube** (set visibility to **Public** or **Unlisted**) or **Google Drive** (set sharing to **Anyone with the link can view**).
  2. Double-check that the video has clear English audio or English subtitles and does **not** exceed 30 seconds.
  3. Paste the public URL into the **Project Demo** field on the Space Apps platform.

---

## 4. Project Details (What, How, Why)

> **Copy & Paste:**
> ### What We Developed
> SAFER is a full-stack geospatial decision-support platform designed for farmers, agronomists, and rural cooperatives. Users can upload a `.kml` or GeoJSON field boundary, select preloaded real-world sample farms, or draw their parcel directly on an interactive satellite map. Within seconds, SAFER builds an agroclimatic diagnosis, a 3D Farm Digital Twin, and a ranked Top-3 list of 3-cycle annual crop rotation strategies tailored to the field's conditions and the farmer's goals.
>
> ### How It Addresses the Challenge
> Farmers worldwide face shifting rainfall patterns, prolonged droughts, heat waves, and soil degradation caused by repeated monoculture. Translating raw satellite and climate datasets into practical planting decisions usually requires specialized GIS skills. SAFER bridges that gap by:
> 1. **Diagnosing local climate & hazard risk** using NASA POWER historical/daily weather, NASA GIBS satellite layers (NDVI and GPM IMERG precipitation), NASA CMR STAC Sentinel-2 imagery metadata, and NASA EONET regional natural events.
> 2. **Integrating local soil & farmer priorities** by querying ISRIC SoilGrids v2.0 (texture, pH, organic carbon) while allowing manual overrides from field lab tests, and weighting five farmer priorities (Water Conservation, Soil Health & Organic Matter, Natural Pest Control, Economic Stability, and Climate Resilience).
> 3. **Recommending & stress-testing rotations** via a Multi-Criteria Decision Analysis (MCDA) engine (`RotationEngineService`) that enforces botanical-family rotation rules to break pest cycles, balances nitrogen and crop water demand, estimates fungal/insect pest risk (`PestRiskService`), and compares the #1 rotation against monoculture under a **Normal Year**, **Severe Drought (-25% rainfall)**, and **Heat Wave (+2.0°C)**.
>
> ### Why It Is Important
> By turning complex orbital observations into an intuitive, field-ready action plan, SAFER empowers smallholder and commercial farmers—such as the Central American farms modeled in our test cases—to protect soil organic matter, reduce water deficit risk, break pest cycles naturally, and stabilize yields under climate stress.

---

## 5. NASA & Space Agency Data Used

> **Copy & Paste:**
> - **NASA POWER API (`power.larc.nasa.gov`):** Used for point-based climatology and daily time-series data including corrected precipitation (`PRECTOTCORR`), mean/max/min air temperature (`T2M`, `T2M_MAX`, `T2M_MIN`), all-sky surface shortwave downward irradiance (`ALLSKY_SFC_SW_DWN`), and relative humidity (`RH2M`).
> - **NASA CMR STAC (`cmr.earthdata.nasa.gov/stac/LPCLOUD/search`):** Used to query Harmonized Landsat Sentinel-2 (`HLSS30.v2.0`) scenes over the user's parcel bounding box, displaying acquisition dates and cloud cover percentages.
> - **NASA GIBS WMTS (`gibs.earthdata.nasa.gov`):** Used to render live satellite raster layers (`MODIS_Terra_NDVI_8Day` and `IMERG_Precipitation_Rate`) directly over the parcel polygon in the map viewer.
> - **NASA EONET (`NasaEonetService`):** Used to detect and surface nearby natural events within a ~65 km bounding box around the farm's coordinates.
> - **Partner / Complementary Open Data:** **ISRIC SoilGrids v2.0** (`rest.isric.org`) for baseline 0–5 cm soil texture, pH (`phh2o`), and organic carbon (`soc`), plus terrain elevation grid sampling for the 3D Farm Digital Twin.

---

## 6. Technical Details (Tools, Languages, Software & Architecture)

> **Copy & Paste:**
> - **Backend (`.NET 10` / `C#` / `ASP.NET Core Web API`):** Built with `Safer.Api` exposing REST endpoints (`/api/parcel`, `/api/earthdata`, `/api/rotation`). Implements in-memory caching (`IMemoryCache` with 2–12 hour TTLs) and regional fallbacks for API resilience, a custom KML/GeoJSON geospatial parser (`GeospatialParserService`), a 6×6 terrain elevation grid service (`ElevationService`), a 9-crop agronomic catalog (`CropCatalogService`), a pest-risk assessor (`PestRiskService`), and the MCDA rotation and What-If simulation engine (`RotationEngineService`).
> - **Frontend (`Angular 19.2` / `TypeScript 5.7` / `RxJS` / `SCSS`):** Interactive single-page application featuring a **Leaflet 1.9** satellite map with interactive polygon drawing and NASA GIBS WMTS overlays, a **Three.js 0.187** 3D Farm Digital Twin (`farm-twin-3d.component.ts`), interactive NASA POWER rainfall charts with clickable monthly daily-rain drill-downs, priority sliders, and What-If comparison cards.
> - **Testing & Deployment:** Unit tests in `Safer.Tests` covering geospatial parsing and rotation logic, plus a multi-stage `Dockerfile` (`mcr.microsoft.com/dotnet/sdk:10.0` to `aspnet:10.0`) for containerized cloud deployment.

---

## 7. Artificial Intelligence (AI) Disclosure

> **Copy & Paste:**
> During development, the team used **Antigravity** (powered by the **Gemini 4 Hydrogen** model) as a coding and documentation assistant to accelerate backend/frontend implementation, refine English technical documentation, and structure submission materials. All core architectural decisions, agronomic rules, NASA API integrations, and testing were reviewed and validated by the team.

---

## 8. Pre-Submission Checklist

- [ ] **GitHub Repository (`https://github.com/504-Sistemas/SAFER`) is set to Public.**
- [ ] **30-second video (`feadback/SAFER_Pitch_30s_EN.mp4`) is uploaded to YouTube (Public/Unlisted) or Google Drive (Anyone with the link).**
- [ ] **Test all links in an Incognito/Private browser window** to confirm no login or permission prompt appears.
- [ ] **Paste all English sections above** into the NASA Space Apps Challenge submission form and save/submit before the deadline.
