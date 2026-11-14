# SAFER — Herramienta de Soporte a la Decisión para Rotación de Cultivos Resiliente al Clima

## 1. Propósito de la Solución

**SAFER** es una aplicación web geoespacial diseñada para ayudar a agricultores y técnicos agrícolas a elegir **estrategias de rotación de cultivos** que fortalezcan la salud del suelo, conserven el agua y aumenten la resiliencia frente a variaciones de temperatura, lluvias irregulares y eventos extremos.

La herramienta integra **observaciones de la Tierra de la NASA**, **información local del suelo**, **características agronómicas de cultivos** y **prioridades del agricultor** en un flujo interactivo de análisis y simulación.

---

## 2. Arquitectura del Sistema

El proyecto está dividido en dos aplicaciones independientes:

- **Backend (`backend/Safer.Api` — ASP.NET Core .NET 10):** Expone la API REST encargada del procesamiento geoespacial (KML/GeoJSON), la integración con servicios de la NASA y SoilGrids, el motor de recomendación multi-criterio (MCDA) y el simulador de escenarios *What-If*.
- **Frontend (`frontend/` — Angular 19 + Leaflet):** Interfaz web interactiva con visor satelital, carga/dibujo de parcelas, ajuste de suelo y prioridades, tablero climático NASA, tarjetas de rotación y comparador visual de escenarios.

---

## 3. Funcionalidad Principal

### 3.1. Carga y Delimitación Geoespacial de Parcelas
- **Muestras integradas:** Permite cargar con un clic los polígonos de prueba ubicados en `feadback/` (`Finca1.kml` y `Finca2.kml`).
- **Carga de archivos KML / GeoJSON:** El usuario puede subir su propio archivo `.kml` o enviar geometrías GeoJSON.
- **Dibujo interactivo en mapa:** Desde el visor Leaflet, el agricultor puede activar el modo *"+ Dibujar Parcela"*, marcar los vértices directamente sobre la imagen satelital y cerrar el polígono.
- **Cálculo automático:** `GeospatialParserService` calcula el **centroide**, el **bounding box** y el **área geodésica en hectáreas**.

### 3.2. Diagnóstico Agroclimático y Satelital
Al analizar una parcela, el sistema genera un perfil completo con:
- **KPIs climáticos:** Precipitación anual (mm), cantidad de meses secos, temperatura media/máxima, riesgo de déficit hídrico y estrés térmico.
- **Perfil mensual de precipitación:** Gráfica de barras con la distribución mensual de lluvias según datos históricos de la NASA.
- **Escenas Sentinel-2 (HLS):** Listado de escenas recientes disponibles para el *bounding box* de la finca, indicando fecha de adquisición y porcentaje de nubosidad.
- **Capas satelitales en vivo sobre el mapa:** Botones para activar o desactivar capas visuales de **NASA GIBS** (*NDVI 8-Días MODIS* y *Tasa de Precipitación GPM IMERG*) encima del polígono de la parcela.

### 3.3. Suelo Local y Prioridades del Agricultor
- **Perfil base de suelo + ajuste manual:** Se consulta automáticamente la textura, pH, carbono orgánico (%) y drenaje según la coordenada de la finca, permitiendo al agricultor activar *"Ajustar suelo manualmente"* para ingresar datos de su propio análisis de campo.
- **Cultivo base (Monocultivo de referencia):** Selección del cultivo actual para usarlo como línea base en la comparación.
- **5 Prioridades ponderables (0% a 100%):**
  1. Conservación de Agua
  2. Salud del Suelo y Materia Orgánica
  3. Control Natural de Plagas
  4. Estabilidad Económica
  5. Resiliencia Climática

### 3.4. Motor de Recomendación de Rotaciones (Top 3)
`RotationEngineService` evalúa combinaciones de **3 ciclos anuales** (*Ciclo 1: Primera/Lluvias → Ciclo 2: Postrera → Ciclo 3: Época Seca/Cobertura*) aplicando reglas agronómicas y análisis multi-criterio (MCDA):
- **Ruptura de plagas:** Prohíbe repetir la misma familia botánica en ciclos consecutivos.
- **Balance hídrico y térmico:** Penaliza secuencias cuya demanda hídrica exceda la disponibilidad climática de la zona o que no toleren las temperaturas máximas ni el pH del suelo.
- **Recuperación de fertilidad:** Premia la alternancia entre cultivos extractores de nitrógeno, leguminosas fijadoras y cultivos de cobertura que aportan materia orgánica.
- **Salida:** Entrega las **3 mejores estrategias** ordenadas por puntaje global (`0–100`), desglosando eficiencia hídrica, salud del suelo, control de plagas, resiliencia climática, balance neto de nitrógeno y una justificación por cada ciclo.

### 3.5. Simulador de Escenarios "What-If"
Compara el desempeño proyectado del **Monocultivo actual** frente a la **Estrategia de Rotación #1** bajo tres escenarios climáticos:
1. **Año Climático Normal** (línea base NASA POWER).
2. **Sequía Severa** (`-25%` de precipitación).
3. **Ola de Calor** (`+2.0°C` en temperatura).

Para cada escenario muestra comparativas visuales de **Estabilidad de Rendimiento (%)**, **Índice de Salud del Suelo** y **Ahorro Hídrico Estimado (%)**.

---

## 4. Fuentes de Datos Consumidas

| Fuente / API | Uso en SAFER | Endpoint / Tecnología | Resiliencia / Caché |
| :--- | :--- | :--- | :--- |
| **NASA POWER Climatology API** | Precipitación (`PRECTOTCORR`), temperaturas (`T2M`, `T2M_MAX`, `T2M_MIN`), radiación solar (`ALLSKY_SFC_SW_DWN`) y humedad relativa (`RH2M`). | `https://power.larc.nasa.gov/api/temporal/climatology/point` | Caché en memoria (`6 horas`) + *fallback* regional si hay fallo de red. |
| **NASA CMR STAC (`HLSS30.v2.0`)** | Búsqueda de escenas **Harmonized Landsat Sentinel-2** sobre el *bounding box* de la finca (fechas y nubosidad). | `https://cmr.earthdata.nasa.gov/stac/LPCLOUD/search` | Caché en memoria (`2 horas`); soporta token opcional de NASA Earthdata Login (`NasaEarthdata:Token`). |
| **NASA GIBS (WMTS)** | Capas raster visuales superpuestas en el mapa Leaflet (`MODIS_Terra_NDVI_8Day`, `IMERG_Precipitation_Rate`). | `https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/...` | Consumo directo desde el cliente web sin autenticación. |
| **ISRIC SoilGrids v2.0** | Propiedades base del suelo (`0-5cm`): arcilla, arena, limo, pH (`phh2o`) y carbono orgánico (`soc`). | `https://rest.isric.org/soilgrids/v2.0/properties/query` | Timeout de `5 s`, caché (`12 horas`), *fallback* regional y ajuste manual por el usuario. |
| **Catálogo Agronómico SAFER** | Características de 9 cultivos (Maíz, Frijol Común, Sorgo, Soya, Crotalaria, Avena, Yuca, Camote, Girasol): familia botánica, $K_c$, demanda hídrica, balance de N, profundidad radicular, rangos de pH/temperatura y tolerancia a sequía/calor. | `CropCatalogService.cs` (Backend .NET) | Disponible vía `GET /api/rotation/crops`. |
| **Polígonos del Usuario / Muestras** | Geometrías de parcelas en formato KML o GeoJSON. | `feadback/*.kml`, carga de archivo o dibujo en mapa | Procesado por `GeospatialParserService.cs`. |

---

## 5. Endpoints del Backend (`Safer.Api`)

- `GET /api/parcel/samples` — Lista los archivos `.kml` de muestra en `feadback/`.
- `GET /api/parcel/samples/{fileName}` — Parsea y devuelve la geometría y métricas de una muestra.
- `POST /api/parcel/parse-kml` — Parsea un archivo `.kml` enviado como `multipart/form-data`.
- `POST /api/parcel/parse-geojson` — Parsea un GeoJSON enviado en el cuerpo de la petición.
- `POST /api/earthdata/analyze` — Ejecuta el diagnóstico NASA POWER + CMR STAC + SoilGrids para una parcela.
- `GET /api/rotation/crops` — Devuelve el catálogo agronómico de cultivos.
- `POST /api/rotation/recommend` — Ejecuta el análisis completo y devuelve diagnóstico, Top 3 rotaciones y escenarios *What-If*.
