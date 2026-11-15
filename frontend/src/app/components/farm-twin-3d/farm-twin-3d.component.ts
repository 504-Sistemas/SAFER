import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import type * as THREE from 'three';
import type { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { I18nService } from '../../services/i18n.service';
import {
  EonetEventDto,
  ParcelTerrainDto,
  ParsedParcelDto,
  RecentWeatherAnomalyDto,
  RotationRecommendationDto,
  ScenarioMetricPoint,
  SoilProfileDto
} from '../../models/safer.models';

type HazardMode = 'AUTO' | 'LANDSLIDE' | 'FLOOD' | 'DROUGHT_HEAT';

@Component({
  selector: 'app-farm-twin-3d',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (isExpanded) {
      <div class="twin-backdrop" (click)="toggleExpand(false)"></div>
    }
    <div class="twin-card" [class.expanded]="isExpanded">
      <div class="twin-header">
        <div>
          <h3>{{ i18n.t('Vista 3D de tu Finca (Gemelo Digital)', '3D Farm View (Digital Twin)') }}</h3>
          <p class="twin-sub">
            {{ i18n.t('Relieve real del terreno (NASA/SRTM)', 'Real terrain relief (NASA/SRTM)') }} ·
            @if (terrain) {
              <span>{{ terrain.meanElevationM }} m · {{ i18n.t('Inclinación', 'Slope') }} {{ terrain.estimatedSlopePct }}% ({{ i18n.tr(terrain.erosionRisk) }})</span>
            }
          </p>
        </div>

        <div class="twin-mode-pills">
          <button
            type="button"
            class="twin-pill"
            [class.active]="viewMode === 'rotation'"
            (click)="setViewMode('rotation')">
            {{ i18n.t('Plan', 'Plan') }} #{{ selectedRotation?.rank ?? 1 }}
          </button>
          <button
            type="button"
            class="twin-pill"
            [class.active]="viewMode === 'monoculture'"
            (click)="setViewMode('monoculture')">
            {{ i18n.t('Siempre lo mismo', 'Monoculture') }} ({{ i18n.tr(baselineCropName) }})
          </button>
          <button type="button" class="twin-pill reset-btn" (click)="resetCamera()">
            {{ i18n.t('Centrar', 'Reset View') }}
          </button>
          <button
            type="button"
            class="twin-pill expand-btn"
            [class.active]="isExpanded"
            (click)="toggleExpand()">
            {{ isExpanded ? i18n.t('Contraer', 'Exit Full View') : i18n.t('Expandir 3D', 'Expand 3D') }}
          </button>
        </div>
      </div>

      <!-- Hazard & Natural Disaster Simulator -->
      <div class="hazard-bar">
        <span class="cycle-caption">{{ i18n.t('Simular riesgo:', 'Simulate risk:') }}</span>
        <button
          type="button"
          class="hazard-btn"
          [class.active]="hazardMode === 'AUTO'"
          (click)="setHazardMode('AUTO')">
          {{ i18n.t('Clima actual / What-If', 'Current / What-If') }}
        </button>
        <button
          type="button"
          class="hazard-btn danger"
          [class.active]="hazardMode === 'LANDSLIDE'"
          (click)="setHazardMode('LANDSLIDE')">
          {{ i18n.t('Lluvia fuerte / Deslave', 'Heavy Rain / Landslide') }}
        </button>
        <button
          type="button"
          class="hazard-btn water"
          [class.active]="hazardMode === 'FLOOD'"
          (click)="setHazardMode('FLOOD')">
          {{ i18n.t('Inundación (parte baja)', 'Flooding (low basin)') }}
        </button>
        <button
          type="button"
          class="hazard-btn heat"
          [class.active]="hazardMode === 'DROUGHT_HEAT'"
          (click)="setHazardMode('DROUGHT_HEAT')">
          {{ i18n.t('Sequía / Calor extremo', 'Drought / Extreme Heat') }}
        </button>
      </div>

      @if (viewMode === 'rotation' && selectedRotation?.cycles?.length) {
        <div class="cycle-selector">
          <span class="cycle-caption">{{ i18n.t('Etapa de siembra:', 'Planting cycle:') }}</span>
          @for (c of selectedRotation!.cycles; track c.cycleNumber; let i = $index) {
            <button
              type="button"
              class="cycle-btn"
              [class.active]="selectedCycleIndex === i"
              (click)="setCycleIndex(i)">
              {{ c.cycleNumber }}{{ i18n.t('ª', 'x') }}: {{ i18n.tr(c.crop.name) }}
            </button>
          }
        </div>
      }

      <div class="canvas-wrap">
        <div #rendererContainer class="renderer-host"></div>

        @if (webglError) {
          <div class="webgl-fallback">
            {{ i18n.t('No se pudo iniciar la vista 3D en este navegador', 'Could not initialize 3D view in this browser') }} ({{ webglError }}).
          </div>
        } @else {
          <div class="hud-overlay">
            <div class="hud-badge" [class.warn]="effectiveScenario !== 'NORMAL'" [class.danger]="hazardMode === 'LANDSLIDE'" [class.water]="hazardMode === 'FLOOD'">
              {{ statusBannerText }}
            </div>

            <div class="hud-crop">
              <strong>{{ i18n.t('Cultivo en 3D:', '3D Crop:') }}</strong> {{ currentCropLabel }}
              @if (activeScenario) {
                <span> · {{ i18n.t('Cosecha esperada:', 'Expected yield stability:') }} {{ currentStabilityPct }}%</span>
              }
            </div>

            @if (primaryEonetAlert; as alert) {
              <div class="hud-eonet">
                <span class="pulse-dot"></span>
                <span><strong>{{ i18n.t('Alerta NASA EONET', 'NASA EONET Alert') }} ({{ alert.distanceKm }} km):</strong> {{ i18n.tr(alert.title) }}</span>
              </div>
            } @else if (recentAnomaly && recentAnomaly.anomalyPct >= 40) {
              <div class="hud-anomaly">
                <span><strong>{{ i18n.t('Lluvia reciente NASA:', 'NASA Recent Rain:') }}</strong> +{{ recentAnomaly.anomalyPct }}% {{ i18n.t('sobre lo normal (suelo húmedo)', 'above normal (wet soil)') }}</span>
              </div>
            }
          </div>

          @if (hazardExplanation) {
            <div class="hud-bottom-note">
              {{ hazardExplanation }}
            </div>
          }
        }
      </div>

      <div class="twin-footer">
        <div class="legend-items">
          <span class="leg"><i class="dot border"></i> {{ i18n.t('Borde de tu finca', 'Parcel boundary') }}</span>
          <span class="leg"><i class="dot healthy"></i> {{ i18n.t('Cultivo fuerte', 'Healthy crop') }}</span>
          <span class="leg"><i class="dot stressed"></i> {{ i18n.t('Cultivo con estrés', 'Stressed crop') }}</span>
          @if (hazardMode === 'LANDSLIDE') {
            <span class="leg"><i class="dot landslide"></i> {{ i18n.t('Zona de deslave / arrastre', 'Landslide / runoff zone') }}</span>
          } @else if (hazardMode === 'FLOOD') {
            <span class="leg"><i class="dot water"></i> {{ i18n.t('Acumulación de agua', 'Water pooling') }}</span>
          } @else if ((terrain?.estimatedSlopePct ?? 0) >= 5) {
            <span class="leg"><i class="dot water"></i> {{ i18n.t('Caída de agua (pendiente)', 'Runoff direction (slope)') }}</span>
          }
        </div>
        <span class="drag-hint">{{ i18n.t('Arrastra para girar · Rueda para acercar', 'Drag to rotate · Scroll to zoom') }}</span>
      </div>
    </div>
  `,
  styles: [`
    .twin-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(15, 23, 42, 0.62);
      z-index: 1990;
    }
    .twin-card {
      background: #ffffff;
      border: 1px solid #d9e2ec;
      border-radius: 14px;
      padding: 14px 16px;
      margin-bottom: 16px;
      box-shadow: 0 2px 10px rgba(15, 23, 42, 0.04);
    }
    .twin-card.expanded {
      position: fixed;
      inset: 18px;
      z-index: 2000;
      margin: 0;
      display: flex;
      flex-direction: column;
      box-shadow: 0 24px 60px rgba(15, 23, 42, 0.45);
    }
    .twin-card.expanded .canvas-wrap {
      flex: 1;
      height: auto;
      min-height: 420px;
    }
    .twin-pill.expand-btn {
      background: #f0fdf4;
      border-color: #86efac;
      color: #166534;
    }
    .twin-pill.expand-btn.active {
      background: #1e293b;
      border-color: #1e293b;
      color: #ffffff;
    }
    .twin-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 12px;
      flex-wrap: wrap;
      margin-bottom: 10px;
    }
    .twin-header h3 {
      margin: 0;
      font-size: 1rem;
      color: #102a43;
    }
    .twin-sub {
      margin: 2px 0 0;
      font-size: 0.8rem;
      color: #486581;
    }
    .twin-mode-pills {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
    }
    .twin-pill {
      border: 1px solid #cbd5e1;
      background: #f8fafc;
      color: #334e68;
      border-radius: 999px;
      padding: 5px 11px;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .twin-pill.active {
      background: #0f766e;
      border-color: #0f766e;
      color: #ffffff;
    }
    .twin-pill.reset-btn {
      background: #eef2ff;
      border-color: #c7d2fe;
      color: #3730a3;
    }
    .hazard-bar, .cycle-selector {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
      margin-bottom: 8px;
    }
    .cycle-caption {
      font-size: 0.78rem;
      font-weight: 600;
      color: #486581;
      margin-right: 4px;
    }
    .hazard-btn, .cycle-btn {
      border: 1px solid #d9e2ec;
      background: #f0f4f8;
      color: #243b53;
      border-radius: 8px;
      padding: 4px 10px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .hazard-btn.active {
      background: #0f766e;
      border-color: #0f766e;
      color: #ffffff;
    }
    .hazard-btn.danger.active {
      background: #c2410c;
      border-color: #c2410c;
      color: #ffffff;
    }
    .hazard-btn.water.active {
      background: #0369a1;
      border-color: #0369a1;
      color: #ffffff;
    }
    .hazard-btn.heat.active {
      background: #b45309;
      border-color: #b45309;
      color: #ffffff;
    }
    .cycle-btn.active {
      background: #15803d;
      border-color: #15803d;
      color: #ffffff;
    }
    .canvas-wrap {
      position: relative;
      width: 100%;
      height: 325px;
      border-radius: 12px;
      overflow: hidden;
      background: linear-gradient(180deg, #dbeafe 0%, #eff6ff 60%, #e2e8f0 100%);
      border: 1px solid #cbd5e1;
    }
    .renderer-host {
      width: 100%;
      height: 100%;
      display: block;
      overflow: hidden;
    }
    :host ::ng-deep .renderer-host canvas {
      width: 100% !important;
      height: 100% !important;
      display: block;
    }
    .hud-overlay {
      position: absolute;
      top: 10px;
      left: 10px;
      right: 10px;
      display: flex;
      flex-direction: column;
      gap: 5px;
      pointer-events: none;
    }
    .hud-badge {
      display: inline-block;
      width: fit-content;
      background: rgba(15, 118, 110, 0.92);
      color: #fff;
      font-size: 0.74rem;
      font-weight: 700;
      padding: 4px 10px;
      border-radius: 999px;
    }
    .hud-badge.warn {
      background: rgba(180, 83, 9, 0.92);
    }
    .hud-badge.danger {
      background: rgba(194, 65, 12, 0.94);
    }
    .hud-badge.water {
      background: rgba(3, 105, 161, 0.94);
    }
    .hud-crop, .hud-eonet, .hud-anomaly {
      width: fit-content;
      max-width: 92%;
      background: rgba(255, 255, 255, 0.92);
      color: #102a43;
      font-size: 0.74rem;
      padding: 4px 9px;
      border-radius: 8px;
      box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
    }
    .hud-eonet {
      background: rgba(254, 242, 242, 0.95);
      border: 1px solid #fca5a5;
      color: #991b1b;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .hud-anomaly {
      background: rgba(239, 246, 255, 0.94);
      border: 1px solid #93c5fd;
      color: #1e40af;
    }
    .pulse-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #dc2626;
      flex-shrink: 0;
      animation: eonetPulse 1.4s infinite;
    }
    @keyframes eonetPulse {
      0%, 100% { transform: scale(1); opacity: 1; }
      50% { transform: scale(1.45); opacity: 0.55; }
    }
    .hud-bottom-note {
      position: absolute;
      bottom: 8px;
      left: 10px;
      right: 10px;
      background: rgba(15, 23, 42, 0.78);
      color: #f8fafc;
      font-size: 0.73rem;
      padding: 5px 10px;
      border-radius: 8px;
      pointer-events: none;
    }
    .webgl-fallback {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 100%;
      padding: 16px;
      color: #486581;
      font-size: 0.85rem;
      text-align: center;
    }
    .twin-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 10px;
      flex-wrap: wrap;
      margin-top: 8px;
      font-size: 0.75rem;
      color: #486581;
    }
    .legend-items {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      align-items: center;
    }
    .leg {
      display: inline-flex;
      align-items: center;
      gap: 5px;
    }
    .dot {
      width: 9px;
      height: 9px;
      border-radius: 50%;
      display: inline-block;
    }
    .dot.border { background: #10b981; }
    .dot.healthy { background: #16a34a; }
    .dot.stressed { background: #ca8a04; }
    .dot.landslide { background: #dc2626; }
    .dot.water { background: #0284c7; }
    .drag-hint {
      font-style: italic;
      color: #627d98;
    }
  `]
})
export class FarmTwin3dComponent implements AfterViewInit, OnChanges, OnDestroy {
  readonly i18n = inject(I18nService);
  @ViewChild('rendererContainer', { static: true }) containerRef!: ElementRef<HTMLDivElement>;

  @Input() parcel?: ParsedParcelDto | null;
  @Input() terrain?: ParcelTerrainDto | null;
  @Input() soil?: SoilProfileDto | null;
  @Input() selectedRotation?: RotationRecommendationDto | null;
  @Input() baselineCropName = 'Maíz';
  @Input() scenarioCode = 'NORMAL';
  @Input() activeScenario?: ScenarioMetricPoint | null;
  @Input() eonetEvents: EonetEventDto[] = [];
  @Input() recentAnomaly?: RecentWeatherAnomalyDto | null;

  viewMode: 'rotation' | 'monoculture' = 'rotation';
  hazardMode: HazardMode = 'AUTO';
  selectedCycleIndex = 0;
  isExpanded = false;
  webglError: string | null = null;

  private THREE?: typeof THREE;
  private scene?: THREE.Scene;
  private camera?: THREE.PerspectiveCamera;
  private renderer?: THREE.WebGLRenderer;
  private controls?: OrbitControls;
  private worldGroup?: THREE.Group;
  private animFrameId?: number;
  private resizeObserver?: ResizeObserver;

  // Dynamic hazard animation state
  private particlePoints?: THREE.Points;
  private particleVelocities: { vx: number; vy: number; vz: number; startX: number; startY: number; startZ: number }[] = [];
  private floodMesh?: THREE.Mesh;
  private floodBaseY = 0;
  private clock = 0;

  private readonly worldSpan = 6.0;

  get effectiveScenario(): string {
    if (this.hazardMode === 'DROUGHT_HEAT') return 'DROUGHT';
    if (this.hazardMode === 'LANDSLIDE' || this.hazardMode === 'FLOOD') return 'HAZARD';
    return this.scenarioCode;
  }

  get primaryEonetAlert(): EonetEventDto | null {
    return this.eonetEvents?.length ? this.eonetEvents[0] : null;
  }

  get statusBannerText(): string {
    const slope = this.terrain?.estimatedSlopePct ?? 7.5;
    if (this.hazardMode === 'LANDSLIDE') {
      return this.i18n.t(
        `Simulando: Lluvia fuerte y riesgo de deslave (${slope}% pendiente)`,
        `Simulating: Heavy rain & landslide risk (${slope}% slope)`
      );
    }
    if (this.hazardMode === 'FLOOD') {
      return this.i18n.t(
        `Simulando: Inundación en parte baja (${this.soil?.drainage ?? 'Drenaje normal'})`,
        `Simulating: Low-basin flooding (${this.i18n.tr(this.soil?.drainage ?? 'Drenaje normal')})`
      );
    }
    if (this.hazardMode === 'DROUGHT_HEAT' || this.scenarioCode === 'DROUGHT') {
      return this.i18n.t('Simulando: Sequía fuerte (-25% lluvia)', 'Simulating: Severe drought (-25% rain)');
    }
    if (this.scenarioCode === 'HEATWAVE') {
      return this.i18n.t('Simulando: Ola de Calor (+2°C)', 'Simulating: Heatwave (+2°C)');
    }
    return this.i18n.t('Simulando: Año Normal', 'Simulating: Normal Year');
  }

  get hazardExplanation(): string | null {
    const slope = this.terrain?.estimatedSlopePct ?? 7.5;
    const rank = this.selectedRotation?.rank ?? 1;
    if (this.hazardMode === 'LANDSLIDE') {
      return this.viewMode === 'monoculture'
        ? this.i18n.t(
            `Pendiente de ${slope}% sin rotación: la lluvia arrastra tierra fértil cuesta abajo. Cambia a "Plan #${rank}" para ver cómo la cobertura protege el suelo.`,
            `${slope}% slope without rotation: heavy rain washes fertile topsoil downhill. Switch to "Plan #${rank}" to see how cover crops protect the soil.`
          )
        : this.i18n.t(
            `Con el Plan #${rank}, las raíces y cobertura vegetal frenan el arrastre de tierra en esta pendiente de ${slope}%.`,
            `With Plan #${rank}, deep roots and vegetative cover reduce soil erosion on this ${slope}% slope.`
          );
    }
    if (this.hazardMode === 'FLOOD') {
      const minElev = this.terrain?.minElevationM ?? 820;
      return this.i18n.t(
        `En lluvias intensas, el agua baja hacia la zona más honda del terreno (${minElev} msnm). Evita cultivos sensibles al encharcamiento allí.`,
        `During heavy storms, runoff pools in the lowest elevation basin (${minElev} m). Avoid waterlogging-sensitive crops there.`
      );
    }
    if (this.hazardMode === 'DROUGHT_HEAT') {
      return this.viewMode === 'monoculture'
        ? this.i18n.t(
            'Sin rotación, la tierra pierde humedad rápido y el cultivo sufre estrés severo por falta de agua.',
            'Without rotation, soil loses moisture rapidly and monoculture crops suffer severe water stress.'
          )
        : this.i18n.t(
            'La rotación conserva mejor la humedad y materia orgánica del suelo frente a sequía y calor.',
            'Crop rotation retains soil moisture and organic matter far better under drought and heat.'
          );
    }
    return null;
  }

  get currentCropLabel(): string {
    if (this.viewMode === 'monoculture') {
      return `${this.i18n.tr(this.baselineCropName)} (${this.i18n.t('sin rotar', 'no rotation')})`;
    }
    const cycles = this.selectedRotation?.cycles ?? [];
    const step = cycles[this.selectedCycleIndex] ?? cycles[0];
    return step
      ? `${this.i18n.tr(step.crop.name)} (${step.cycleNumber}${this.i18n.t('ª siembra', 'x cycle')})`
      : this.i18n.tr(this.baselineCropName);
  }

  get currentStabilityPct(): number {
    if (!this.activeScenario) return 80;
    return this.viewMode === 'monoculture'
      ? this.activeScenario.monocultureYieldStabilityPct
      : this.activeScenario.rotationYieldStabilityPct;
  }

  async ngAfterViewInit(): Promise<void> {
    await this.initThree();
    this.rebuildScene();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedRotation']) {
      this.selectedCycleIndex = 0;
    }
    if (this.scene) {
      this.rebuildScene();
    }
  }

  ngOnDestroy(): void {
    if (this.animFrameId !== undefined) {
      cancelAnimationFrame(this.animFrameId);
    }
    this.resizeObserver?.disconnect();
    this.controls?.dispose();
    this.disposeGroup(this.worldGroup);
    this.renderer?.dispose();
  }

  setViewMode(mode: 'rotation' | 'monoculture'): void {
    this.viewMode = mode;
    this.rebuildScene();
  }

  setHazardMode(mode: HazardMode): void {
    this.hazardMode = mode;
    this.rebuildScene();
  }

  setCycleIndex(idx: number): void {
    this.selectedCycleIndex = idx;
    this.rebuildScene();
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.isExpanded) {
      this.toggleExpand(false);
    }
  }

  toggleExpand(force?: boolean): void {
    this.isExpanded = force !== undefined ? force : !this.isExpanded;
    requestAnimationFrame(() => this.handleResize());
    setTimeout(() => this.handleResize(), 60);
  }

  resetCamera(): void {
    if (!this.camera || !this.controls) return;
    this.camera.position.set(5.4, 4.4, 5.6);
    this.controls.target.set(0, 0.35, 0);
    this.controls.update();
  }

  private async initThree(): Promise<void> {
    const container = this.containerRef.nativeElement;
    const width = Math.max(300, container.clientWidth || 560);
    const height = Math.max(240, container.clientHeight || 325);

    try {
      const [threeMod, controlsMod] = await Promise.all([
        import('three'),
        import('three/examples/jsm/controls/OrbitControls.js')
      ]);
      const THREE = threeMod;
      this.THREE = THREE;

      this.scene = new THREE.Scene();

      this.camera = new THREE.PerspectiveCamera(42, width / height, 0.1, 100);
      this.camera.position.set(5.4, 4.4, 5.6);

      this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
      this.renderer.setSize(width, height, false);
      this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
      this.renderer.shadowMap.enabled = true;
      this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
      container.innerHTML = '';
      container.appendChild(this.renderer.domElement);

      this.controls = new controlsMod.OrbitControls(this.camera, this.renderer.domElement);
      this.controls.enableDamping = true;
      this.controls.dampingFactor = 0.08;
      this.controls.maxPolarAngle = Math.PI / 2.15;
      this.controls.minDistance = 3.5;
      this.controls.maxDistance = 13;
      this.controls.target.set(0, 0.35, 0);
      this.controls.update();

      const ambient = new THREE.AmbientLight(0xffffff, 0.72);
      this.scene.add(ambient);

      const sun = new THREE.DirectionalLight(0xfff7ed, 1.15);
      sun.position.set(6, 9, 4);
      sun.castShadow = true;
      sun.shadow.mapSize.width = 1024;
      sun.shadow.mapSize.height = 1024;
      this.scene.add(sun);

      const fill = new THREE.DirectionalLight(0xbae6fd, 0.35);
      fill.position.set(-5, 3, -5);
      this.scene.add(fill);

      this.worldGroup = new THREE.Group();
      this.scene.add(this.worldGroup);

      this.resizeObserver = new ResizeObserver(() => this.handleResize());
      this.resizeObserver.observe(container);

      const animate = () => {
        this.animFrameId = requestAnimationFrame(animate);
        this.clock += 0.025;
        this.animateHazards();
        this.controls?.update();
        if (this.renderer && this.scene && this.camera) {
          this.renderer.render(this.scene, this.camera);
        }
      };
      animate();
    } catch (err) {
      this.webglError = err instanceof Error ? err.message : 'WebGL no disponible';
    }
  }

  private animateHazards(): void {
    if (this.particlePoints && this.particleVelocities.length > 0) {
      const posAttr = this.particlePoints.geometry.getAttribute('position') as THREE.BufferAttribute;
      for (let i = 0; i < this.particleVelocities.length; i++) {
        const v = this.particleVelocities[i];
        let x = posAttr.getX(i) + v.vx;
        let y = posAttr.getY(i) + v.vy;
        let z = posAttr.getZ(i) + v.vz;

        if (y < 0.24 || Math.abs(x) > 3.2 || Math.abs(z) > 3.2) {
          x = v.startX;
          y = v.startY;
          z = v.startZ;
        }
        posAttr.setXYZ(i, x, y, z);
      }
      posAttr.needsUpdate = true;
    }

    if (this.floodMesh) {
      this.floodMesh.position.y = this.floodBaseY + Math.sin(this.clock * 1.8) * 0.015;
    }
  }

  private handleResize(): void {
    if (!this.renderer || !this.camera) return;
    const el = this.containerRef.nativeElement;
    const w = Math.max(200, el.clientWidth);
    const h = Math.max(200, el.clientHeight);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h, false);
  }

  private rebuildScene(): void {
    const THREE = this.THREE;
    if (!THREE || !this.worldGroup || !this.parcel) return;

    this.particlePoints = undefined;
    this.particleVelocities = [];
    this.floodMesh = undefined;
    this.disposeGroup(this.worldGroup);

    const bbox = this.parcel.boundingBox;
    const latSpan = Math.max(0.0005, bbox.maxLat - bbox.minLat);
    const lonSpan = Math.max(0.0005, bbox.maxLon - bbox.minLon);
    const aspect = Math.min(1.8, Math.max(0.55, lonSpan / latSpan));
    const widthX = aspect >= 1 ? this.worldSpan : this.worldSpan * aspect;
    const depthZ = aspect >= 1 ? this.worldSpan / aspect : this.worldSpan;

    const gridSize = this.terrain?.gridSize && this.terrain.gridSize >= 2 ? this.terrain.gridSize : 6;
    const rawGrid = this.terrain?.elevationGrid?.length === gridSize * gridSize
      ? this.terrain.elevationGrid
      : this.buildFlatFallbackGrid(gridSize);

    const minE = Math.min(...rawGrid);
    const maxE = Math.max(...rawGrid);
    const elevRange = Math.max(1, maxE - minE);
    const slopePct = this.terrain?.estimatedSlopePct ?? 5;
    const maxReliefHeight = Math.min(1.8, Math.max(0.22, (slopePct / 20) * 1.45));

    const toLocal = (lat: number, lon: number) => {
      const u = (lon - bbox.minLon) / lonSpan;
      const v = (lat - bbox.minLat) / latSpan;
      const x = (u - 0.5) * widthX;
      const z = (0.5 - v) * depthZ;
      return { u, v, x, z };
    };

    const sampleHeight = (u: number, v: number): number => {
      const cu = Math.min(1, Math.max(0, u));
      const cv = Math.min(1, Math.max(0, v));
      const fx = cu * (gridSize - 1);
      const fy = cv * (gridSize - 1);
      const c0 = Math.floor(fx);
      const r0 = Math.floor(fy);
      const c1 = Math.min(gridSize - 1, c0 + 1);
      const r1 = Math.min(gridSize - 1, r0 + 1);
      const tx = fx - c0;
      const ty = fy - r0;

      const e00 = rawGrid[r0 * gridSize + c0];
      const e10 = rawGrid[r0 * gridSize + c1];
      const e01 = rawGrid[r1 * gridSize + c0];
      const e11 = rawGrid[r1 * gridSize + c1];

      const top = e00 * (1 - tx) + e10 * tx;
      const bot = e01 * (1 - tx) + e11 * tx;
      const elev = top * (1 - ty) + bot * ty;
      return ((elev - minE) / elevRange) * maxReliefHeight + 0.25;
    };

    // 1. Terrain Surface Mesh (24x24 smooth subdivisions)
    const segs = 24;
    const terrainGeo = new THREE.PlaneGeometry(widthX, depthZ, segs, segs);
    terrainGeo.rotateX(-Math.PI / 2);

    const pos = terrainGeo.attributes['position'];
    const colors = new Float32Array(pos.count * 3);

    const baseTerrainColor = this.getScenarioSoilColor();
    const outsideColor = new THREE.Color('#94a3b8');
    const landslideDangerColor = new THREE.Color(this.viewMode === 'monoculture' ? '#dc2626' : '#eab308');
    const poly = this.parcel.coordinates;

    let minParcelY = Infinity;
    let maxParcelY = -Infinity;

    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i);
      const z = pos.getZ(i);
      const u = x / widthX + 0.5;
      const v = 0.5 - z / depthZ;
      const y = sampleHeight(u, v);
      pos.setY(i, y);

      const lat = bbox.minLat + v * latSpan;
      const lon = bbox.minLon + u * lonSpan;
      const inside = this.isPointInPolygon(lat, lon, poly);

      if (inside) {
        if (y < minParcelY) minParcelY = y;
        if (y > maxParcelY) maxParcelY = y;
      }

      const shade = 0.85 + 0.28 * ((y - 0.25) / Math.max(0.2, maxReliefHeight));
      let c = inside ? baseTerrainColor.clone().multiplyScalar(shade) : outsideColor.clone().multiplyScalar(shade * 0.92);

      // Highlight steep landslide-prone zones when simulating LANDSLIDE
      if (inside && this.hazardMode === 'LANDSLIDE') {
        const du = 0.04;
        const gx = (sampleHeight(u + du, v) - sampleHeight(u - du, v)) / (2 * du);
        const gy = (sampleHeight(u, v + du) - sampleHeight(u, v - du)) / (2 * du);
        const localSteepness = Math.min(1, Math.sqrt(gx * gx + gy * gy) / 1.4);
        const blendFactor = this.viewMode === 'monoculture' ? localSteepness * 0.88 : localSteepness * 0.45;
        c = c.lerp(landslideDangerColor, blendFactor);
      }

      colors[i * 3] = c.r;
      colors[i * 3 + 1] = c.g;
      colors[i * 3 + 2] = c.b;
    }

    terrainGeo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    terrainGeo.computeVertexNormals();

    const terrainMat = new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: this.hazardMode === 'FLOOD' || this.hazardMode === 'LANDSLIDE' ? 0.65 : 0.88,
      metalness: 0.05
    });
    const terrainMesh = new THREE.Mesh(terrainGeo, terrainMat);
    terrainMesh.receiveShadow = true;
    this.worldGroup.add(terrainMesh);

    // 2. Soil Slab Base
    const baseGeo = new THREE.BoxGeometry(widthX, 0.26, depthZ);
    const baseMat = new THREE.MeshStandardMaterial({ color: '#5c4033', roughness: 0.95 });
    const baseMesh = new THREE.Mesh(baseGeo, baseMat);
    baseMesh.position.y = 0.12;
    this.worldGroup.add(baseMesh);

    // 3. Parcel Polygon Outline on 3D surface
    if (poly.length >= 3) {
      const linePts: THREE.Vector3[] = [];
      for (const pt of poly) {
        const { u, v, x, z } = toLocal(pt.latitude, pt.longitude);
        const y = sampleHeight(u, v) + 0.045;
        linePts.push(new THREE.Vector3(x, y, z));
      }
      const first = poly[0];
      const fLocal = toLocal(first.latitude, first.longitude);
      linePts.push(new THREE.Vector3(fLocal.x, sampleHeight(fLocal.u, fLocal.v) + 0.045, fLocal.z));

      const lineGeo = new THREE.BufferGeometry().setFromPoints(linePts);
      const borderHex = this.hazardMode === 'LANDSLIDE' ? '#f97316' : '#10b981';
      const lineMat = new THREE.LineBasicMaterial({ color: borderHex, linewidth: 2 });
      const borderLine = new THREE.Line(lineGeo, lineMat);
      this.worldGroup.add(borderLine);
    }

    // 4. Stylized 3D Crops inside the parcel polygon
    this.addCropsInsideParcel(poly, bbox, latSpan, lonSpan, toLocal, sampleHeight);

    // 5. Flood Basin Overlay (when FLOOD mode is active)
    if (this.hazardMode === 'FLOOD' && Number.isFinite(minParcelY) && Number.isFinite(maxParcelY)) {
      this.addFloodPlane(widthX, depthZ, minParcelY, maxParcelY);
    }

    // 6. Downhill Landslide/Runoff Particles (when LANDSLIDE mode is active)
    if (this.hazardMode === 'LANDSLIDE') {
      this.addLandslideParticles(poly, bbox, latSpan, lonSpan, toLocal, sampleHeight);
    }

    // 7. Water Runoff Arrow if terrain has noticeable slope (>= 5%)
    if (slopePct >= 5) {
      this.addRunoffArrows(rawGrid, gridSize, widthX, depthZ, sampleHeight);
    }
  }

  private addFloodPlane(widthX: number, depthZ: number, minParcelY: number, maxParcelY: number): void {
    const THREE = this.THREE;
    if (!THREE || !this.worldGroup) return;

    this.floodBaseY = minParcelY + Math.max(0.12, (maxParcelY - minParcelY) * 0.36);
    const waterGeo = new THREE.PlaneGeometry(widthX * 0.96, depthZ * 0.96);
    waterGeo.rotateX(-Math.PI / 2);
    const waterMat = new THREE.MeshStandardMaterial({
      color: '#0284c7',
      transparent: true,
      opacity: 0.56,
      roughness: 0.15,
      metalness: 0.25
    });
    this.floodMesh = new THREE.Mesh(waterGeo, waterMat);
    this.floodMesh.position.y = this.floodBaseY;
    this.worldGroup.add(this.floodMesh);
  }

  private addLandslideParticles(
    poly: { latitude: number; longitude: number }[],
    bbox: { minLat: number; maxLat: number; minLon: number; maxLon: number },
    latSpan: number,
    lonSpan: number,
    toLocal: (lat: number, lon: number) => { u: number; v: number; x: number; z: number },
    sampleHeight: (u: number, v: number) => number
  ): void {
    const THREE = this.THREE;
    if (!THREE || !this.worldGroup || poly.length < 3) return;

    const coords: number[] = [];
    const step = 9;
    const speedScale = this.viewMode === 'monoculture' ? 0.022 : 0.011;

    for (let r = 1; r < step; r++) {
      for (let c = 1; c < step; c++) {
        const u = c / step;
        const v = r / step;
        const lat = bbox.minLat + v * latSpan;
        const lon = bbox.minLon + u * lonSpan;
        if (!this.isPointInPolygon(lat, lon, poly)) continue;

        const { x, z } = toLocal(lat, lon);
        const y = sampleHeight(u, v) + 0.07;

        const du = 0.04;
        const dhdu = (sampleHeight(u + du, v) - sampleHeight(u - du, v)) / (2 * du);
        const dhdv = (sampleHeight(u, v + du) - sampleHeight(u, v - du)) / (2 * du);

        // Downhill direction: -gradient in (u -> +x, v -> -z)
        const vx = -dhdu * speedScale;
        const vz = dhdv * speedScale;
        const vy = -0.008;

        coords.push(x, y, z);
        this.particleVelocities.push({ vx, vy, vz, startX: x, startY: y, startZ: z });
      }
    }

    if (coords.length === 0) return;

    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(coords, 3));

    const mat = new THREE.PointsMaterial({
      color: this.viewMode === 'monoculture' ? '#b45309' : '#38bdf8',
      size: this.viewMode === 'monoculture' ? 0.14 : 0.1,
      transparent: true,
      opacity: 0.9
    });

    this.particlePoints = new THREE.Points(geo, mat);
    this.worldGroup.add(this.particlePoints);
  }

  private addCropsInsideParcel(
    poly: { latitude: number; longitude: number }[],
    bbox: { minLat: number; maxLat: number; minLon: number; maxLon: number },
    latSpan: number,
    lonSpan: number,
    toLocal: (lat: number, lon: number) => { u: number; v: number; x: number; z: number },
    sampleHeight: (u: number, v: number) => number
  ): void {
    const THREE = this.THREE;
    if (!THREE || !this.worldGroup || poly.length < 3) return;

    const cropInfo = this.getActiveCropStyle();
    const healthFactor = this.getHealthFactor();

    const step = 11;
    const plantsGroup = new THREE.Group();

    const stemMat = new THREE.MeshStandardMaterial({
      color: cropInfo.stemColor.clone().lerp(new THREE.Color('#a16207'), 1 - healthFactor),
      roughness: 0.8
    });
    const leafMat = new THREE.MeshStandardMaterial({
      color: cropInfo.leafColor.clone().lerp(new THREE.Color('#ca8a04'), 1 - healthFactor),
      roughness: 0.72
    });

    for (let r = 1; r < step; r++) {
      for (let c = 1; c < step; c++) {
        const u = c / step;
        const v = r / step;
        const lat = bbox.minLat + v * latSpan;
        const lon = bbox.minLon + u * lonSpan;

        if (!this.isPointInPolygon(lat, lon, poly)) continue;

        if (this.viewMode === 'monoculture' && healthFactor < 0.65 && (r + c) % 3 === 0) {
          continue;
        }

        const { x, z } = toLocal(lat, lon);
        const y = sampleHeight(u, v);

        const plant = new THREE.Group();
        const scaleH = (0.55 + 0.55 * healthFactor) * cropInfo.heightScale;

        if (cropInfo.shape === 'tall') {
          const stalk = new THREE.Mesh(new THREE.CylinderGeometry(0.025, 0.035, 0.34 * scaleH, 6), stemMat);
          stalk.position.y = 0.17 * scaleH;
          stalk.castShadow = true;
          plant.add(stalk);

          const crown = new THREE.Mesh(new THREE.ConeGeometry(0.11, 0.22 * scaleH, 6), leafMat);
          crown.position.y = 0.32 * scaleH;
          crown.castShadow = true;
          plant.add(crown);
        } else if (cropInfo.shape === 'bush') {
          const bush = new THREE.Mesh(new THREE.DodecahedronGeometry(0.11 * scaleH, 0), leafMat);
          bush.position.y = 0.09 * scaleH;
          bush.castShadow = true;
          plant.add(bush);
        } else {
          const pad = new THREE.Mesh(new THREE.CylinderGeometry(0.13, 0.15, 0.07 * scaleH, 7), leafMat);
          pad.position.y = 0.035 * scaleH;
          pad.castShadow = true;
          plant.add(pad);
        }

        plant.position.set(x, y, z);
        plantsGroup.add(plant);
      }
    }

    this.worldGroup.add(plantsGroup);
  }

  private addRunoffArrows(
    grid: number[],
    gridSize: number,
    widthX: number,
    depthZ: number,
    sampleHeight: (u: number, v: number) => number
  ): void {
    const THREE = this.THREE;
    if (!THREE || !this.worldGroup) return;

    let minIdx = 0;
    let maxIdx = 0;
    for (let i = 1; i < grid.length; i++) {
      if (grid[i] < grid[minIdx]) minIdx = i;
      if (grid[i] > grid[maxIdx]) maxIdx = i;
    }

    const rMax = Math.floor(maxIdx / gridSize);
    const cMax = maxIdx % gridSize;
    const rMin = Math.floor(minIdx / gridSize);
    const cMin = minIdx % gridSize;

    const uHigh = cMax / (gridSize - 1);
    const vHigh = rMax / (gridSize - 1);
    const uLow = cMin / (gridSize - 1);
    const vLow = rMin / (gridSize - 1);

    const start = new THREE.Vector3(
      (uHigh - 0.5) * widthX,
      sampleHeight(uHigh, vHigh) + 0.28,
      (0.5 - vHigh) * depthZ
    );
    const end = new THREE.Vector3(
      (uLow - 0.5) * widthX,
      sampleHeight(uLow, vLow) + 0.28,
      (0.5 - vLow) * depthZ
    );

    const dir = end.clone().sub(start);
    const len = dir.length();
    if (len < 0.2) return;

    const arrow = new THREE.ArrowHelper(
      dir.normalize(),
      start.lerp(end, 0.25),
      Math.min(2.2, len * 0.5),
      this.hazardMode === 'LANDSLIDE' ? 0xf97316 : 0x0284c7,
      0.28,
      0.16
    );
    this.worldGroup.add(arrow);
  }

  private getScenarioSoilColor(): THREE.Color {
    const THREE = this.THREE!;
    if (this.hazardMode === 'DROUGHT_HEAT' || this.scenarioCode === 'DROUGHT') return new THREE.Color('#b5894c');
    if (this.scenarioCode === 'HEATWAVE') return new THREE.Color('#b96d3b');
    if (this.hazardMode === 'LANDSLIDE') return new THREE.Color('#6b5b3e');
    if (this.hazardMode === 'FLOOD') return new THREE.Color('#3b6e5c');
    return new THREE.Color('#4d7c4f');
  }

  private getHealthFactor(): number {
    if (this.hazardMode === 'DROUGHT_HEAT') {
      return this.viewMode === 'monoculture' ? 0.42 : 0.78;
    }
    if (this.hazardMode === 'LANDSLIDE') {
      return this.viewMode === 'monoculture' ? 0.55 : 0.88;
    }
    if (this.activeScenario) {
      const pct = this.viewMode === 'monoculture'
        ? this.activeScenario.monocultureYieldStabilityPct
        : this.activeScenario.rotationYieldStabilityPct;
      return Math.min(1, Math.max(0.28, pct / 100));
    }
    if (this.scenarioCode === 'DROUGHT') return this.viewMode === 'monoculture' ? 0.45 : 0.78;
    if (this.scenarioCode === 'HEATWAVE') return this.viewMode === 'monoculture' ? 0.52 : 0.82;
    return this.viewMode === 'monoculture' ? 0.76 : 0.95;
  }

  private getActiveCropStyle(): {
    shape: 'tall' | 'bush' | 'cover';
    heightScale: number;
    stemColor: THREE.Color;
    leafColor: THREE.Color;
  } {
    const THREE = this.THREE!;
    let cropName = this.baselineCropName.toLowerCase();
    let category = 'cereal';

    if (this.viewMode === 'rotation' && this.selectedRotation?.cycles?.length) {
      const step = this.selectedRotation.cycles[this.selectedCycleIndex] ?? this.selectedRotation.cycles[0];
      cropName = step.crop.name.toLowerCase();
      category = step.crop.category.toLowerCase();
    }

    if (cropName.includes('maíz') || cropName.includes('sorgo') || category.includes('cereal')) {
      return {
        shape: 'tall',
        heightScale: 1.15,
        stemColor: new THREE.Color('#4d7c0f'),
        leafColor: new THREE.Color('#16a34a')
      };
    }

    if (cropName.includes('cobertura') || cropName.includes('abono') || category.includes('cobertura')) {
      return {
        shape: 'cover',
        heightScale: 0.9,
        stemColor: new THREE.Color('#15803d'),
        leafColor: new THREE.Color('#22c55e')
      };
    }

    return {
      shape: 'bush',
      heightScale: 1.0,
      stemColor: new THREE.Color('#3f6212'),
      leafColor: new THREE.Color('#15803d')
    };
  }

  private buildFlatFallbackGrid(size: number): number[] {
    const arr: number[] = [];
    for (let r = 0; r < size; r++) {
      for (let c = 0; c < size; c++) {
        arr.push(800 + r * 4 + c * 3);
      }
    }
    return arr;
  }

  private isPointInPolygon(lat: number, lon: number, poly: { latitude: number; longitude: number }[]): boolean {
    let inside = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const xi = poly[i].longitude, yi = poly[i].latitude;
      const xj = poly[j].longitude, yj = poly[j].latitude;

      const intersect = ((yi > lat) !== (yj > lat))
        && (lon < (xj - xi) * (lat - yi) / ((yj - yi) || 1e-12) + xi);
      if (intersect) inside = !inside;
    }
    return inside;
  }

  private disposeGroup(group?: THREE.Group): void {
    if (!group) return;
    while (group.children.length > 0) {
      const obj = group.children[0];
      group.remove(obj);
      obj.traverse((child) => {
        if ((child as THREE.Mesh).geometry) {
          (child as THREE.Mesh).geometry.dispose();
        }
        const mat = (child as THREE.Mesh).material;
        if (Array.isArray(mat)) {
          mat.forEach(m => m.dispose());
        } else if (mat) {
          mat.dispose();
        }
      });
    }
  }
}
