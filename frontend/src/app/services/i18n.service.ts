import { Injectable } from '@angular/core';

export type AppLang = 'es' | 'en';

@Injectable({
  providedIn: 'root'
})
export class I18nService {
  private readonly storageKey = 'safer_lang';
  lang: AppLang = 'es';

  private readonly termMap: Record<string, string> = {
    // Risk & Erosion levels
    'Bajo': 'Low',
    'Moderado': 'Moderate',
    'Alto': 'High',
    'Riesgo Bajo': 'Low Risk',
    'Riesgo Medio': 'Medium Risk',
    'Riesgo Alto': 'High Risk',

    // Soil textures & drainage
    'Franco': 'Loam',
    'Franco Arcilloso': 'Clay Loam',
    'Franco Arenoso': 'Sandy Loam',
    'Arcilloso': 'Clay',
    'Buen drenaje': 'Well drained',
    'Drenaje normal': 'Normal drainage',
    'Se encharca fácil': 'Poorly drained',

    // Crops
    'Maíz': 'Corn / Maize',
    'Frijol Común': 'Common Bean',
    'Sorgo': 'Sorghum',
    'Soya': 'Soybean',
    'Crotalaria (Abono Verde)': 'Sunn Hemp (Green Manure)',
    'Avena Forrajera / Cobertura': 'Forage Oats / Cover Crop',
    'Yuca': 'Cassava',
    'Camote': 'Sweet Potato',
    'Girasol': 'Sunflower',

    // Seasons
    '1ª Siembra (Época de Lluvias)': '1st Cycle (Rainy Season)',
    '2ª Siembra (Postrera)': '2nd Cycle (Late Rainy)',
    '3ª Siembra (Época Seca / Cobertura)': '3rd Cycle (Dry / Cover)',

    // Scenarios
    'Año Normal de Lluvias': 'Normal Rainfall Year',
    'Si hay Sequía fuerte (-25% lluvia)': 'Severe Drought (-25% rain)',
    'Si hace mucho Calor (+2°C)': 'Extreme Heatwave (+2°C)',

    // Weather anomalies
    'Más seco de lo normal': 'Drier than normal',
    'Normal': 'Normal',
    'Más húmedo de lo normal': 'Wetter than normal',

    // EONET categories, titles & statuses
    'Severe Storms': 'Severe Storms',
    'Floods': 'Floods',
    'Wildfires': 'Wildfires',
    'Inundación en ': 'Flood in ',
    'Incendio en ': 'Wildfire in ',
    'Tormenta fuerte ': 'Severe Storm ',
    'Tormenta tropical ': 'Tropical Storm ',
    'Huracán ': 'Hurricane ',
    'Sequía en ': 'Drought in ',
    'Evento climático en la región': 'Regional climate event',
    'Inundación': 'Flood',
    'Incendio': 'Wildfire',
    'Tormenta fuerte': 'Severe Storm',
    'Sequía': 'Drought',
    'Deslizamiento': 'Landslide',
    'Alerta climática': 'Climate alert',
    'Evento pasado': 'Past event',
    'Vigente ahora': 'Active now',

    // Months
    'Ene': 'Jan', 'Feb': 'Feb', 'Mar': 'Mar', 'Abr': 'Apr',
    'May': 'May', 'Jun': 'Jun', 'Jul': 'Jul', 'Ago': 'Aug',
    'Sep': 'Sep', 'Oct': 'Oct', 'Nov': 'Nov', 'Dic': 'Dec'
  };

  constructor() {
    try {
      const saved = localStorage.getItem(this.storageKey);
      if (saved === 'en' || saved === 'es') {
        this.lang = saved;
      }
    } catch {
      // Ignore storage errors in restricted contexts
    }
  }

  setLang(lang: AppLang): void {
    this.lang = lang;
    try {
      localStorage.setItem(this.storageKey, lang);
    } catch {
      // Ignore storage errors
    }
  }

  t(es: string, en: string): string {
    return this.lang === 'en' ? en : es;
  }

  tr(value: string | null | undefined): string {
    if (!value) return '';
    if (this.lang === 'es') return value;

    if (this.termMap[value]) {
      return this.termMap[value];
    }

    // Translate composite strings like strategyName ("Camote → Crotalaria ... → Girasol")
    let out = value;
    for (const [es, en] of Object.entries(this.termMap)) {
      if (out.includes(es)) {
        out = out.split(es).join(en);
      }
    }
    return out;
  }
}
