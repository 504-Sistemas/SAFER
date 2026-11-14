import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import type * as THREE from 'three';
import type { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import {
  ParcelTerrainDto,
  ParsedParcelDto,
  RotationRecommendationDto,
  ScenarioMetricPoint,
  SoilProfileDto
} from '../../models/safer.models';

@Component({
  selector: 'app-farm-twin-3d',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="twin-card">
      <div class="twin-header">
        <div>
          <h3>Vista 3D de tu Finca (Gemelo Digital)</h3>
          <p class="twin-sub">
            Relieve real del terreno (NASA/SRTM) ·
            @if (terrain) {
              <span>{{ terrain.meanElevationM }} msnm · Inclinación {{ terrain.estimatedSlopePct }}% ({{ terrain.erosionRisk }})</span>
            }
          </p>
        </div>

        <div class="twin-mode-pills">
          <button
            type="button"
            class="twin-pill"
            [class.active]="viewMode === 'rotation'"
            (click)="setViewMode('rotation')">
            Plan #{{ selectedRotation?.rank ?? 1 }}
          </button>
          <button
            type="button"
            class="twin-pill"
            [class.active]="viewMode === 'monoculture'"
            (click)="setViewMode('monoculture')">
            Siempre lo mismo ({{ baselineCropName }})
          </button>
          <button type="button" class="twin-pill reset-btn" (click)="resetCamera()">
            Centrar
          </button>
        </div>
      </div>

      @if (viewMode === 'rotation' && selectedRotation?.cycles?.length) {
        <div class="cycle-selector">
          <span class="cycle-caption">Etapa de siembra:</span>
          @for (c of selectedRotation!.cycles; track c.cycleNumber; let i = $index) {
            <button
              type="button"
              class="cycle-btn"
              [class.active]="selectedCycleIndex === i"
              (click)="setCycleIndex(i)">
              {{ c.cycleNumber }}ª: {{ c.crop.name }}
            </button>
          }
        </div>
      }

      <div class="canvas-wrap">
        <div #rendererContainer class="renderer-host"></div>

        @if (webglError) {
          <div class="webgl-fallback">
            No se pudo iniciar la vista 3D en este navegador ({{ webglError }}).
          </div>
        } @else {
          <div class="hud-overlay">
            <div class="hud-badge" [class.warn]="scenarioCode !== 'NORMAL'">
              @if (scenarioCode === 'DROUGHT') {
                Simulando: Sequía (-25% lluvia)
              } @else if (scenarioCode === 'HEATWAVE') {
                Simulando: Ola de Calor (+2°C)
              } @else {
                Simulando: Año Normal
              }
            </div>
            <div class="hud-crop">
              <strong>Cultivo en 3D:</strong> {{ currentCropLabel }}
              @if (activeScenario) {
                <span> · Cosecha esperada: {{ currentStabilityPct }}%</span>
              }
            </div>
          </div>
        }
      </div>

      <div class="twin-footer">
        <div class="legend-items">
          <span class="leg"><i class="dot border"></i> Borde de tu finca</span>
          <span class="leg"><i class="dot healthy"></i> Cultivo fuerte</span>
          <span class="leg"><i class="dot stressed"></i> Cultivo con estrés</span>
          @if ((terrain?.estimatedSlopePct ?? 0) >= 5) {
            <span class="leg"><i class="dot water"></i> Caída de agua (pendiente)</span>
          }
        </div>
        <span class="drag-hint">Arrastra para girar · Rueda para acercar</span>
      </div>
    </div>
  `,
  styles: [`
    .twin-card {
      background: #ffffff;
      border: 1px solid #d9e2ec;
      border-radius: 14px;
      padding: 14px 16px;
      margin-bottom: 16px;
      box-shadow: 0 2px 10px rgba(15, 23, 42, 0.04);
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
    .cycle-selector {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
      margin-bottom: 10px;
    }
    .cycle-caption {
      font-size: 0.78rem;
      font-weight: 600;
      color: #486581;
      margin-right: 4px;
    }
    .cycle-btn {
      border: 1px solid #d9e2ec;
      background: #f0f4f8;
      color: #243b53;
      border-radius: 8px;
      padding: 4px 10px;
      font-size: 0.76rem;
      font-weight: 600;
      cursor: pointer;
    }
    .cycle-btn.active {
      background: #15803d;
      border-color: #15803d;
      color: #ffffff;
    }
    .canvas-wrap {
      position: relative;
      width: 100%;
      height: 310px;
      border-radius: 12px;
      overflow: hidden;
      background: linear-gradient(180deg, #dbeafe 0%, #eff6ff 60%, #e2e8f0 100%);
      border: 1px solid #cbd5e1;
    }
    .renderer-host {
      width: 100%;
      height: 100%;
      display: block;
    }
    .hud-overlay {
      position: absolute;
      top: 10px;
      left: 10px;
      display: flex;
      flex-direction: column;
      gap: 5px;
      pointer-events: none;
    }
    .hud-badge {
      display: inline-block;
      width: fit-content;
      background: rgba(15, 118, 110, 0.9);
      color: #fff;
      font-size: 0.74rem;
      font-weight: 700;
      padding: 4px 9px;
      border-radius: 999px;
    }
    .hud-badge.warn {
      background: rgba(180, 83, 9, 0.92);
    }
    .hud-crop {
      background: rgba(255, 255, 255, 0.9);
      color: #102a43;
      font-size: 0.75rem;
      padding: 4px 9px;
      border-radius: 8px;
      box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
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
    .dot.water { background: #0284c7; }
    .drag-hint {
      font-style: italic;
      color: #627d98;
    }
  `]
})
export class FarmTwin3dComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('rendererContainer', { static: true }) containerRef!: ElementRef<HTMLDivElement>;

  @Input() parcel?: ParsedParcelDto | null;
  @Input() terrain?: ParcelTerrainDto | null;
  @Input() soil?: SoilProfileDto | null;
  @Input() selectedRotation?: RotationRecommendationDto | null;
  @Input() baselineCropName = 'Maíz';
  @Input() scenarioCode = 'NORMAL';
  @Input() activeScenario?: ScenarioMetricPoint | null;

  viewMode: 'rotation' | 'monoculture' = 'rotation';
  selectedCycleIndex = 0;
  webglError: string | null = null;

  private THREE?: typeof THREE;
  private scene?: THREE.Scene;
  private camera?: THREE.PerspectiveCamera;
  private renderer?: THREE.WebGLRenderer;
  private controls?: OrbitControls;
  private worldGroup?: THREE.Group;
  private animFrameId?: number;
  private resizeObserver?: ResizeObserver;

  private readonly worldSpan = 6.0;

  get currentCropLabel(): string {
    if (this.viewMode === 'monoculture') {
      return `${this.baselineCropName} (sin rotar)`;
    }
    const cycles = this.selectedRotation?.cycles ?? [];
    const step = cycles[this.selectedCycleIndex] ?? cycles[0];
    return step ? `${step.crop.name} (${step.cycleNumber}ª siembra)` : this.baselineCropName;
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

  setCycleIndex(idx: number): void {
    this.selectedCycleIndex = idx;
    this.rebuildScene();
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
    const height = Math.max(240, container.clientHeight || 310);

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
      this.renderer.setSize(width, height);
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

      // Lighting
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

  private handleResize(): void {
    if (!this.renderer || !this.camera) return;
    const el = this.containerRef.nativeElement;
    const w = Math.max(200, el.clientWidth);
    const h = Math.max(200, el.clientHeight);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h);
  }

  private rebuildScene(): void {
    const THREE = this.THREE;
    if (!THREE || !this.worldGroup || !this.parcel) return;
    this.disposeGroup(this.worldGroup);

    const bbox = this.parcel.boundingBox;
    const latSpan = Math.max(0.0005, bbox.maxLat - bbox.minLat);
    const lonSpan = Math.max(0.0005, bbox.maxLon - bbox.minLon);
    const aspect = Math.min(1.8, Math.max(0.55, lonSpan / latSpan));
    const widthX = aspect >= 1 ? this.worldSpan : this.worldSpan * aspect;
    const depthZ = aspect >= 1 ? this.worldSpan / aspect : this.worldSpan;

    const gridSize = this.terrain?.gridSize && this.terrain.gridSize >= 2 ? this.terrain.gridSize : 8;
    const rawGrid = this.terrain?.elevationGrid?.length === gridSize * gridSize
      ? this.terrain.elevationGrid
      : this.buildFlatFallbackGrid(gridSize);

    const minE = Math.min(...rawGrid);
    const maxE = Math.max(...rawGrid);
    const elevRange = Math.max(1, maxE - minE);
    const slopePct = this.terrain?.estimatedSlopePct ?? 5;
    const maxReliefHeight = Math.min(1.8, Math.max(0.22, (slopePct / 20) * 1.45));

    const toLocal = (lat: number, lon: number) => {
      const u = (lon - bbox.minLon) / lonSpan; // 0..1 west->east
      const v = (lat - bbox.minLat) / latSpan; // 0..1 south->north
      const x = (u - 0.5) * widthX;
      const z = (0.5 - v) * depthZ; // north at -Z
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
    const poly = this.parcel.coordinates;

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

      const shade = 0.85 + 0.28 * ((y - 0.25) / Math.max(0.2, maxReliefHeight));
      const c = inside ? baseTerrainColor.clone().multiplyScalar(shade) : outsideColor.clone().multiplyScalar(shade * 0.92);
      colors[i * 3] = c.r;
      colors[i * 3 + 1] = c.g;
      colors[i * 3 + 2] = c.b;
    }

    terrainGeo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    terrainGeo.computeVertexNormals();

    const terrainMat = new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.88,
      metalness: 0.04
    });
    const terrainMesh = new THREE.Mesh(terrainGeo, terrainMat);
    terrainMesh.receiveShadow = true;
    this.worldGroup.add(terrainMesh);

    // 2. Soil Slab Base (dark earth pedestal underneath)
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
      // Close ring
      const first = poly[0];
      const fLocal = toLocal(first.latitude, first.longitude);
      linePts.push(new THREE.Vector3(fLocal.x, sampleHeight(fLocal.u, fLocal.v) + 0.045, fLocal.z));

      const lineGeo = new THREE.BufferGeometry().setFromPoints(linePts);
      const lineMat = new THREE.LineBasicMaterial({ color: '#10b981', linewidth: 2 });
      const borderLine = new THREE.Line(lineGeo, lineMat);
      this.worldGroup.add(borderLine);
    }

    // 4. Stylized 3D Crops inside the parcel polygon
    this.addCropsInsideParcel(poly, bbox, latSpan, lonSpan, toLocal, sampleHeight);

    // 5. Water Runoff Arrows if terrain has noticeable slope (>= 5%)
    if (slopePct >= 5) {
      this.addRunoffArrows(rawGrid, gridSize, widthX, depthZ, sampleHeight);
    }
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

        // Skip some plants if monoculture under severe stress
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
          // Ground cover / low broadleaf
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

    // Find general downhill direction from grid corners
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
      0x0284c7,
      0.28,
      0.16
    );
    this.worldGroup.add(arrow);
  }

  private getScenarioSoilColor(): THREE.Color {
    const THREE = this.THREE!;
    if (this.scenarioCode === 'DROUGHT') return new THREE.Color('#b5894c');
    if (this.scenarioCode === 'HEATWAVE') return new THREE.Color('#b96d3b');
    return new THREE.Color('#4d7c4f');
  }

  private getHealthFactor(): number {
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
