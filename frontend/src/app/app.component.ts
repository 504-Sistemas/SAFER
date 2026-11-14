import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SaferApiService } from './services/safer-api.service';
import { I18nService } from './services/i18n.service';
import { ParcelMapComponent } from './components/parcel-map/parcel-map.component';
import { FarmTwin3dComponent } from './components/farm-twin-3d/farm-twin-3d.component';
import {
  CropDefinition,
  DecisionSupportResponseDto,
  FarmerPrioritiesDto,
  MonthlyClimatePoint,
  MonthlyDailyPrecipitationDto,
  ParsedParcelDto,
  SoilOverrideDto
} from './models/safer.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, ParcelMapComponent, FarmTwin3dComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  private readonly api = inject(SaferApiService);
  readonly i18n = inject(I18nService);

  sampleFiles: string[] = [];
  selectedSample = '';
  parcel: ParsedParcelDto | null = null;
  crops: CropDefinition[] = [];
  baselineCropId = 'maize';

  priorities: FarmerPrioritiesDto = {
    waterConservation: 85,
    soilHealth: 90,
    pestControl: 75,
    economicStability: 75,
    climateResilience: 85
  };

  useSoilOverrides = false;
  soilOverrides: SoilOverrideDto = {
    textureClass: 'Franco Arcilloso',
    ph: 6.1,
    organicCarbonPct: 1.7,
    nitrogenLevel: 'Medium',
    drainage: 'Moderate'
  };

  analysisResult: DecisionSupportResponseDto | null = null;
  selectedScenarioCode = 'NORMAL';
  selectedRotationRank = 1;
  loading = false;
  errorMessage = '';

  // Progressive disclosure UI toggles
  showParcelMeta = false;
  showPriorities = false;
  showClimateDetails = false;
  showRainChart = false;
  showPestAndAlerts = false;
  showScenarioInsight = false;
  expandedRotationRanks = new Set<number>();

  // Interactive precipitation explorer
  availableYears = [2025, 2024, 2023, 2022, 2021, 2020];
  selectedPrecipYear: number | 'CLIM' = 'CLIM';
  displayedMonthlyProfile: MonthlyClimatePoint[] = [];
  selectedMonthDetail: MonthlyDailyPrecipitationDto | null = null;
  selectedMonthNumber: number | null = null;
  loadingDaily = false;

  ngOnInit(): void {
    this.api.getCrops().subscribe({
      next: (list) => (this.crops = list),
      error: () => {}
    });

    this.api.listSampleParcels().subscribe({
      next: (files) => {
        this.sampleFiles = files;
        if (files.length > 0) {
          this.loadSample(files[0]);
        }
      },
      error: () => {
        this.errorMessage = 'No se pudo conectar con el backend .NET en http://localhost:5013. Asegúrate de iniciarlo con "dotnet run".';
      }
    });
  }

  loadSample(fileName: string): void {
    this.selectedSample = fileName;
    this.loading = true;
    this.errorMessage = '';
    this.api.loadSampleParcel(fileName).subscribe({
      next: (p) => {
        this.parcel = p;
        this.runAnalysis();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err?.error || 'Error al cargar la finca de muestra.';
      }
    });
  }

  onKmlFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.loading = true;
    this.errorMessage = '';
    this.api.uploadKmlFile(file).subscribe({
      next: (p) => {
        this.parcel = p;
        this.selectedSample = '';
        this.runAnalysis();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err?.error || 'No se pudo procesar el archivo KML.';
      }
    });
  }

  onDrawnGeoJson(geoJson: string): void {
    this.loading = true;
    this.errorMessage = '';
    this.api.parseGeoJson(geoJson, 'Parcela Personalizada').subscribe({
      next: (p) => {
        this.parcel = p;
        this.selectedSample = '';
        this.runAnalysis();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err?.error || 'Error al procesar el polígono dibujado.';
      }
    });
  }

  runAnalysis(): void {
    if (!this.parcel) return;
    this.loading = true;
    this.errorMessage = '';

    this.api.recommendRotations({
      parcel: this.parcel,
      priorities: this.priorities,
      baselineCropId: this.baselineCropId,
      soilOverrides: this.useSoilOverrides ? this.soilOverrides : undefined
    }).subscribe({
      next: (res) => {
        this.analysisResult = res;
        this.selectedRotationRank = res.recommendations[0]?.rank ?? 1;
        this.selectedPrecipYear = 'CLIM';
        this.displayedMonthlyProfile = res.earthObservations.climate.monthlyProfile;
        this.selectedMonthDetail = null;
        this.selectedMonthNumber = null;
        this.expandedRotationRanks.clear();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err?.error || 'Error al ejecutar el análisis con NASA Earthdata.';
      }
    });
  }

  selectRotation(rank: number): void {
    this.selectedRotationRank = rank;
  }

  toggleRotationDetails(rank: number, event: Event): void {
    event.stopPropagation();
    if (this.expandedRotationRanks.has(rank)) {
      this.expandedRotationRanks.delete(rank);
    } else {
      this.expandedRotationRanks.add(rank);
    }
  }

  isRotationExpanded(rank: number): boolean {
    return this.expandedRotationRanks.has(rank);
  }

  get selectedRotation() {
    if (!this.analysisResult?.recommendations?.length) return null;
    return this.analysisResult.recommendations.find(r => r.rank === this.selectedRotationRank)
      ?? this.analysisResult.recommendations[0];
  }

  get activeScenario() {
    const scenarios = this.selectedRotation?.whatIfScenarios ?? this.analysisResult?.whatIfScenarios ?? [];
    if (!scenarios.length) return null;
    return scenarios.find(s => s.scenarioCode === this.selectedScenarioCode) ?? scenarios[0];
  }

  getMaxMonthlyPrecip(): number {
    return Math.max(100, ...this.displayedMonthlyProfile.map(p => p.precipitationMm));
  }

  onPrecipYearChange(): void {
    if (!this.analysisResult) return;
    this.selectedMonthDetail = null;
    this.selectedMonthNumber = null;

    if (this.selectedPrecipYear === 'CLIM') {
      this.displayedMonthlyProfile = this.analysisResult.earthObservations.climate.monthlyProfile;
      return;
    }

    const { latitude, longitude } = this.analysisResult.parcel.centroid;
    this.loadingDaily = true;
    this.api.getAnnualPrecipitation(latitude, longitude, this.selectedPrecipYear).subscribe({
      next: (annual) => {
        this.displayedMonthlyProfile = annual.monthlyProfile;
        this.loadingDaily = false;
      },
      error: () => {
        this.loadingDaily = false;
      }
    });
  }

  selectMonthForDaily(month: number): void {
    if (!this.analysisResult) return;
    const yearToFetch = this.selectedPrecipYear === 'CLIM' ? 2025 : this.selectedPrecipYear;
    const { latitude, longitude } = this.analysisResult.parcel.centroid;

    this.selectedMonthNumber = month;
    this.loadingDaily = true;
    this.api.getMonthlyDailyPrecipitation(latitude, longitude, yearToFetch, month).subscribe({
      next: (daily) => {
        this.selectedMonthDetail = daily;
        this.loadingDaily = false;
      },
      error: () => {
        this.loadingDaily = false;
      }
    });
  }
}
