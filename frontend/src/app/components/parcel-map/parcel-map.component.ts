import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as L from 'leaflet';
import { ParsedParcelDto } from '../../models/safer.models';

@Component({
  selector: 'app-parcel-map',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="map-wrapper">
      <div class="map-toolbar">
        <div class="toolbar-group">
          <button type="button" class="chip" [class.active]="baseMode === 'satellite'" (click)="setBaseLayer('satellite')">
            Satélite
          </button>
          <button type="button" class="chip" [class.active]="baseMode === 'osm'" (click)="setBaseLayer('osm')">
            Mapa de Calles
          </button>
          <button type="button" class="chip nasa" [class.active]="gibsLayer === 'ndvi'" (click)="toggleGibs('ndvi')">
            Verdor del Cultivo
          </button>
          <button type="button" class="chip nasa" [class.active]="gibsLayer === 'rain'" (click)="toggleGibs('rain')">
            Lluvia Satelital
          </button>
          <button type="button" class="chip nasa" [class.active]="gibsLayer === 'lst'" (click)="toggleGibs('lst')">
            Calor del Suelo
          </button>
          <button type="button" class="chip nasa" [class.active]="gibsLayer === 'smap'" (click)="toggleGibs('smap')">
            Humedad del Suelo
          </button>
        </div>

        <div class="toolbar-group">
          @if (!isDrawing) {
            <button type="button" class="chip draw" (click)="startDrawing()">
              + Dibujar Finca
            </button>
          } @else {
            <span class="draw-hint">Clic en mapa ({{ drawPoints.length }} pts)</span>
            <button type="button" class="chip finish" [disabled]="drawPoints.length < 3" (click)="finishDrawing()">
              Cerrar Polígono
            </button>
            <button type="button" class="chip cancel" (click)="cancelDrawing()">
              Cancelar
            </button>
          }
        </div>
      </div>

      <div #mapContainer class="map-canvas"></div>
    </div>
  `,
  styles: [`
    .map-wrapper {
      display: flex;
      flex-direction: column;
      border: 1px solid var(--border);
      border-radius: 10px;
      overflow: hidden;
      background: #fff;
    }
    .map-toolbar {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 8px;
      padding: 8px 10px;
      background: #f8faf9;
      border-bottom: 1px solid var(--border);
    }
    .toolbar-group {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
    .chip {
      border: 1px solid var(--border);
      background: #fff;
      color: var(--text-main);
      font-size: 12px;
      font-weight: 600;
      padding: 5px 10px;
      border-radius: 6px;
      cursor: pointer;
    }
    .chip.active {
      background: var(--accent);
      color: #fff;
      border-color: var(--accent);
    }
    .chip.nasa.active {
      background: var(--nasa-blue);
      border-color: var(--nasa-blue);
      color: #fff;
    }
    .chip.draw {
      border-color: var(--accent);
      color: var(--accent);
    }
    .chip.finish {
      background: var(--accent);
      color: #fff;
      border-color: var(--accent);
    }
    .chip.finish:disabled {
      opacity: 0.45;
      cursor: not-allowed;
    }
    .chip.cancel {
      color: #9b2c2c;
      border-color: #feb2b2;
    }
    .draw-hint {
      font-size: 12px;
      color: var(--text-muted);
      font-weight: 600;
    }
    .map-canvas {
      height: 360px;
      width: 100%;
    }
  `]
})
export class ParcelMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('mapContainer', { static: true }) mapContainer!: ElementRef<HTMLDivElement>;
  @Input() parcel: ParsedParcelDto | null = null;
  @Output() drawnGeoJson = new EventEmitter<string>();

  private map?: L.Map;
  private satelliteLayer?: L.TileLayer;
  private osmLayer?: L.TileLayer;
  private gibsOverlay?: L.TileLayer;
  private parcelLayer?: L.GeoJSON;
  private drawPreviewLayer?: L.LayerGroup;

  baseMode: 'satellite' | 'osm' = 'satellite';
  gibsLayer: 'none' | 'ndvi' | 'rain' | 'lst' | 'smap' = 'none';
  isDrawing = false;
  drawPoints: L.LatLng[] = [];

  ngAfterViewInit(): void {
    this.initMap();
    if (this.parcel) {
      this.renderParcel(this.parcel);
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['parcel'] && this.map && this.parcel) {
      this.renderParcel(this.parcel);
    }
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }

  private initMap(): void {
    this.map = L.map(this.mapContainer.nativeElement, {
      center: [14.58, -87.98],
      zoom: 8
    });

    this.satelliteLayer = L.tileLayer(
      'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      { attribution: 'Tiles © Esri', maxZoom: 19 }
    );

    this.osmLayer = L.tileLayer(
      'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      { attribution: '© OpenStreetMap', maxZoom: 19 }
    );

    this.satelliteLayer.addTo(this.map);
    this.drawPreviewLayer = L.layerGroup().addTo(this.map);

    this.map.on('click', (e: L.LeafletMouseEvent) => {
      if (!this.isDrawing) return;
      this.drawPoints.push(e.latlng);
      this.updateDrawPreview();
    });
  }

  setBaseLayer(mode: 'satellite' | 'osm'): void {
    if (!this.map || !this.satelliteLayer || !this.osmLayer) return;
    this.baseMode = mode;
    if (mode === 'satellite') {
      this.map.removeLayer(this.osmLayer);
      this.satelliteLayer.addTo(this.map);
    } else {
      this.map.removeLayer(this.satelliteLayer);
      this.osmLayer.addTo(this.map);
    }
    this.gibsOverlay?.bringToFront();
  }

  toggleGibs(layerType: 'ndvi' | 'rain' | 'lst' | 'smap'): void {
    if (!this.map) return;
    if (this.gibsOverlay) {
      this.map.removeLayer(this.gibsOverlay);
      this.gibsOverlay = undefined;
    }

    if (this.gibsLayer === layerType) {
      this.gibsLayer = 'none';
      return;
    }

    this.gibsLayer = layerType;
    const layerName = layerType === 'ndvi'
      ? 'MODIS_Terra_NDVI_8Day'
      : layerType === 'rain'
        ? 'IMERG_Precipitation_Rate'
        : layerType === 'lst'
          ? 'MODIS_Terra_Land_Surface_Temp_Day'
          : 'SMAP_L4_Analyzed_Surface_Soil_Moisture';

    const matrixSet = layerType === 'ndvi'
      ? 'GoogleMapsCompatible_Level9'
      : layerType === 'lst'
        ? 'GoogleMapsCompatible_Level7'
        : 'GoogleMapsCompatible_Level6';

    const maxZoom = layerType === 'ndvi' ? 9 : layerType === 'lst' ? 7 : 6;
    const gibsDate = '2025-10-16';

    this.gibsOverlay = L.tileLayer(
      `https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/${layerName}/default/${gibsDate}/${matrixSet}/{z}/{y}/{x}.png`,
      {
        attribution: 'NASA GIBS Earthdata',
        opacity: 0.65,
        maxNativeZoom: maxZoom,
        maxZoom: 18
      }
    ).addTo(this.map);
  }

  startDrawing(): void {
    this.isDrawing = true;
    this.drawPoints = [];
    this.drawPreviewLayer?.clearLayers();
  }

  cancelDrawing(): void {
    this.isDrawing = false;
    this.drawPoints = [];
    this.drawPreviewLayer?.clearLayers();
  }

  finishDrawing(): void {
    if (this.drawPoints.length < 3) return;
    const coords = this.drawPoints.map(p => [p.lng, p.lat]);
    coords.push(coords[0]);

    const geoJson = JSON.stringify({
      type: 'Feature',
      properties: { name: 'Parcela Dibujada' },
      geometry: {
        type: 'Polygon',
        coordinates: [coords]
      }
    });

    this.isDrawing = false;
    this.drawPoints = [];
    this.drawPreviewLayer?.clearLayers();
    this.drawnGeoJson.emit(geoJson);
  }

  private updateDrawPreview(): void {
    if (!this.drawPreviewLayer) return;
    this.drawPreviewLayer.clearLayers();

    for (const pt of this.drawPoints) {
      L.circleMarker(pt, { radius: 5, color: '#1b6b3a', fillColor: '#fff', fillOpacity: 1 }).addTo(this.drawPreviewLayer);
    }
    if (this.drawPoints.length >= 2) {
      L.polygon(this.drawPoints, { color: '#38a169', weight: 2, dashArray: '5, 5', fillOpacity: 0.2 }).addTo(this.drawPreviewLayer);
    }
  }

  private renderParcel(parcel: ParsedParcelDto): void {
    if (!this.map || !parcel.geoJsonFeature) return;

    if (this.parcelLayer) {
      this.map.removeLayer(this.parcelLayer);
    }

    const geoJson = JSON.parse(parcel.geoJsonFeature);
    this.parcelLayer = L.geoJSON(geoJson, {
      style: {
        color: '#f6ad55',
        weight: 3,
        fillColor: '#48bb78',
        fillOpacity: 0.25
      }
    }).addTo(this.map);

    const bounds = this.parcelLayer.getBounds();
    if (bounds.isValid()) {
      this.map.fitBounds(bounds, { padding: [32, 32], maxZoom: 16 });
    }
  }
}
