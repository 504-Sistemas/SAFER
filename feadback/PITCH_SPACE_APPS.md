# Pitch y Guion de Demo — SAFER (NASA Space Apps)

> Estructurado siguiendo la metodología de 5 bloques y los 5 criterios oficiales de NASA Space Apps (*Impacto, Creatividad, Validez, Relevancia, Presentación*).

---

## 1. Guion del Pitch (Formato 3 Minutos)

| Bloque | Tiempo | Guion / Qué decir | Criterio |
| :--- | :--- | :--- | :--- |
| **1. Gancho + Problema** | **30 s** | *"En zonas rurales de Honduras —como las fincas que analizamos en Lempira y Comayagua— un agricultor de maíz enfrenta lluvias cada vez más irregulares y olas de calor. Si repite monocultivo año tras año, su suelo pierde materia orgánica, el agua no alcanza y una sola sequía puede arruinar toda la temporada."* | **Impacto** |
| **2. Propuesta de Valor** | **15 s** | *"Creamos **SAFER**: ayudamos a los agricultores a diseñar rotaciones de cultivos resilientes al clima en segundos, cruzando datos satelitales de la NASA con su suelo local y sus propias prioridades, sin necesidad de ser expertos en SIG."* | **Presentación** |
| **3. Demo en Vivo** | **75 s** | 1. **Parcela:** Cargamos `Finca1.kml` (o dibujamos un polígono en el mapa) y activamos la capa **NASA GIBS NDVI / GPM Lluvia**.<br>2. **Diagnóstico NASA:** Mostramos la lluvia anual, meses secos, riesgo hídrico y la gráfica interactiva **NASA POWER** (cambiando de año y haciendo clic en un mes para ver los **días con lluvia**), además de las escenas **Sentinel-2 (`HLSS30.v2.0`)** de **NASA CMR STAC**.<br>3. **Prioridades y Rotación:** Ajustamos suelo local y *sliders* (agua, salud del suelo, plagas, economía) → obtenemos el **Top 3 de rotaciones a 3 ciclos**.<br>4. **Simulador What-If:** Comparamos *Monocultivo vs. Rotación #1* en escenario de **Sequía Severa (-25%)** y **Ola de Calor (+2°C)**. | **Relevancia, Validez** |
| **4. Cómo Funciona y Qué Tiene de Nuevo** | **30 s** | *"Nuestro backend en .NET 10 consulta en vivo NASA POWER (climatología y serie diaria), NASA CMR STAC para escenas Sentinel-2 e ISRIC SoilGrids. Un motor multi-criterio (MCDA) prohíbe repetir familias botánicas consecutivas y balancea nitrógeno y demanda hídrica. Lo distinto: no solo da un diagnóstico pasivo, sino que traduce datos orbitales en un plan accionable por ciclos y simulado bajo estrés climático."* | **Validez, Creatividad** |
| **5. Impacto y Cierre** | **30 s** | *"En nuestras simulaciones con datos climáticos reales de Honduras, pasar de monocultivo de maíz a una rotación diversificada con leguminosas y cobertura reduce el déficit hídrico proyectado y estabiliza el rendimiento frente a sequías. Siguiente paso: validación con cooperativas locales e integración de alertas tempranas con token Earthdata."* | **Impacto** |

---

## 2. Estructura de Diapositivas Mínimas (7 Slides)

1. **Portada:** SAFER — Decision-Support de Rotación de Cultivos · Reto de Agricultura Resiliente al Clima.
2. **El Problema:** Agricultores en Centroamérica (`Finca1.kml` / `Finca2.kml`), degradación de suelo y variabilidad de lluvias.
3. **Propuesta de Valor:** De datos orbitales complejos a un plan de rotación en 3 ciclos adaptado a cada finca.
4. **Demo / 4 Vistas Clave:** Mapa KML + NASA GIBS → Perfil NASA POWER interactivo (anual y diario) + Sentinel-2 → Top 3 Rotaciones → Simulador *What-If*.
5. **Datos NASA y Método:** NASA POWER (`PRECTOTCORR`, `T2M`), NASA CMR STAC (`HLSS30.v2.0`), NASA GIBS WMTS, SoilGrids v2.0 + Motor MCDA.
6. **Impacto:** Comparativa cuantitativa Monocultivo vs. Rotación #1 en *Año Normal*, *Sequía (-25%)* y *Ola de Calor (+2°C)*.
7. **Equipo, Próximos Pasos y Enlaces:** Repositorio (`backend/` + `frontend/`), documentación técnica (`DOCUMENTACION_SOLUCION.pdf`) y roadmap de campo.

---

## 3. Autoevaluación del Equipo (Checklist Space Apps)

- **Impacto:**
  - [x] Caso concreto georreferenciado (`Finca1.kml` y `Finca2.kml` en Honduras).
  - [x] Métricas claras de área (hectáreas), ahorro hídrico (%) y estabilidad de rendimiento (%).
- **Creatividad:**
  - [x] Combina en una sola pantalla diagnóstico satelital, perfil diario de lluvia por clic, motor agronómico MCDA y simulador *What-If*.
- **Validez:**
  - [x] Fuentes oficiales citadas (`NASA POWER`, `NASA CMR STAC HLSS30`, `NASA GIBS`, `ISRIC SoilGrids`) con manejo de caché y *fallbacks* transparentes.
  - [x] Límites claros: la extracción directa de píxeles GeoTIFF de HLS requiere token EDL, mientras el perfil de suelo base permite ajuste manual del agricultor.
- **Relevancia:**
  - [x] Responde directamente al reto: sin los datos climáticos/satelitales de la NASA, el motor no puede evaluar riesgo hídrico ni térmico de la parcela.
- **Presentación:**
  - [x] Interfaz visual con tema **Tierra** (alto contraste, legible para campo) y muestras precargadas a un clic para evitar fallos en vivo.
