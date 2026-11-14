export interface GeoCoordinate {
  latitude: number;
  longitude: number;
}

export interface BoundingBox {
  minLon: number;
  minLat: number;
  maxLon: number;
  maxLat: number;
}

export interface ParsedParcelDto {
  name: string;
  sourceFormat: string;
  coordinates: GeoCoordinate[];
  centroid: GeoCoordinate;
  boundingBox: BoundingBox;
  areaHectares: number;
  geoJsonFeature: string;
}

export interface MonthlyClimatePoint {
  month: number;
  monthName: string;
  precipitationMm: number;
  tempMeanC: number;
  tempMaxC: number;
  tempMinC: number;
  solarRadiationKwhM2Day: number;
  relativeHumidityPct: number;
  referenceEt0Mm?: number;
  waterBalanceMm?: number;
}

export interface NasaPowerClimateDto {
  source: string;
  isLiveApi: boolean;
  latitude: number;
  longitude: number;
  annualPrecipitationMm: number;
  annualEt0Mm?: number;
  annualWaterBalanceMm?: number;
  meanTemperatureC: number;
  maxTemperatureC: number;
  minTemperatureC: number;
  meanSolarRadiationKwhM2Day: number;
  meanRelativeHumidityPct: number;
  dryMonthsCount: number;
  waterDeficitRisk: string;
  thermalStressRisk: string;
  monthlyProfile: MonthlyClimatePoint[];
}

export interface SentinelSceneDto {
  sceneId: string;
  collection: string;
  platform: string;
  acquiredAt: string;
  cloudCoverPct?: number | null;
  browseImageUrl?: string | null;
  stacSelfUrl?: string | null;
}

export interface EonetEventDto {
  id: string;
  title: string;
  category: string;
  date: string;
  distanceKm: number;
  status: string;
  sourceUrl?: string | null;
}

export interface PestRiskAssessmentDto {
  fungalDiseaseRisk: string;
  fungalRiskScore: number;
  insectPestRisk: string;
  insectRiskScore: number;
  highRiskMonths: string[];
  monocultureRiskNote: string;
  rotationRiskReductionPct: number;
  scientificDisclaimer: string;
}

export interface ParcelTerrainDto {
  source: string;
  isLiveApi: boolean;
  meanElevationM: number;
  minElevationM: number;
  maxElevationM: number;
  estimatedSlopePct: number;
  erosionRisk: string;
  terrainNote: string;
  gridSize?: number;
  elevationGrid?: number[];
}

export interface RecentWeatherAnomalyDto {
  isLiveApi: boolean;
  precipitation30DaysMm: number;
  normal30DaysMm: number;
  anomalyPct: number;
  meanTemp30DaysC: number;
  statusLabel: string;
  summaryText: string;
}

export interface EarthObservationSummaryDto {
  earthdataTokenConfigured: boolean;
  sentinelQueryLive: boolean;
  sentinelCollection: string;
  sentinelStatusMessage?: string | null;
  recentSentinelScenes: SentinelSceneDto[];
  recentRadarScenes?: SentinelSceneDto[];
  eonetQueryLive: boolean;
  regionalExtremeEvents: EonetEventDto[];
  pestRisk?: PestRiskAssessmentDto;
  terrain?: ParcelTerrainDto;
  recentAnomaly?: RecentWeatherAnomalyDto;
  climate: NasaPowerClimateDto;
  estimatedNdviBaseline: number;
  estimatedSoilMoistureIndex: number;
  recommendedGibsLayers: string[];
}

export interface SoilProfileDto {
  source: string;
  isLiveApi: boolean;
  textureClass: string;
  clayPct: number;
  sandPct: number;
  siltPct: number;
  ph: number;
  organicCarbonPct: number;
  nitrogenLevel: string;
  drainage: string;
}

export interface SoilOverrideDto {
  textureClass?: string;
  ph?: number;
  organicCarbonPct?: number;
  nitrogenLevel?: string;
  drainage?: string;
}

export interface FarmerPrioritiesDto {
  waterConservation: number;
  soilHealth: number;
  pestControl: number;
  economicStability: number;
  climateResilience: number;
}

export interface CropDefinition {
  id: string;
  name: string;
  scientificName: string;
  botanicalFamily: string;
  category: string;
  cycleDays: number;
  waterDemandMm: number;
  kcCoefficient: number;
  nitrogenBalance: number;
  organicMatterContribution: number;
  rootDepth: string;
  droughtTolerance: number;
  heatTolerance: number;
  marketValueIndex: number;
  agronomicRole: string;
}

export interface RotationCycleStep {
  cycleNumber: number;
  seasonLabel: string;
  crop: CropDefinition;
  rationale: string;
}

export interface RotationRecommendationDto {
  rank: number;
  strategyName: string;
  overallScore: number;
  waterEfficiencyScore: number;
  soilHealthScore: number;
  pestBreakScore: number;
  economicScore: number;
  climateResilienceScore: number;
  netNitrogenBalance: number;
  totalWaterDemandMm: number;
  summary: string;
  cycles: RotationCycleStep[];
  whatIfScenarios?: ScenarioMetricPoint[];
}

export interface ScenarioMetricPoint {
  scenarioName: string;
  scenarioCode: string;
  monocultureYieldStabilityPct: number;
  rotationYieldStabilityPct: number;
  monocultureSoilHealthIndex: number;
  rotationSoilHealthIndex: number;
  monocultureWaterUseMm: number;
  rotationWaterUseMm: number;
  waterSavedPct: number;
  keyInsight: string;
}

export interface RecommendationRequestDto {
  parcel: ParsedParcelDto;
  soilOverrides?: SoilOverrideDto;
  priorities: FarmerPrioritiesDto;
  baselineCropId: string;
}

export interface DecisionSupportResponseDto {
  parcel: ParsedParcelDto;
  earthObservations: EarthObservationSummaryDto;
  soil: SoilProfileDto;
  priorities: FarmerPrioritiesDto;
  baselineCropName: string;
  recommendations: RotationRecommendationDto[];
  whatIfScenarios: ScenarioMetricPoint[];
}

export interface DailyPrecipitationPoint {
  date: string;
  dayOfMonth: number;
  precipitationMm: number;
  tempMeanC: number;
}

export interface AnnualMonthlyProfileDto {
  year: number;
  isLiveApi: boolean;
  source: string;
  monthlyProfile: MonthlyClimatePoint[];
}

export interface MonthlyDailyPrecipitationDto {
  year: number;
  month: number;
  monthName: string;
  isLiveApi: boolean;
  source: string;
  totalPrecipitationMm: number;
  rainyDaysCount: number;
  days: DailyPrecipitationPoint[];
}
