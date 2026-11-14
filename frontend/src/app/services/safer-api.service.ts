import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AnnualMonthlyProfileDto,
  CropDefinition,
  DecisionSupportResponseDto,
  MonthlyDailyPrecipitationDto,
  ParsedParcelDto,
  RecommendationRequestDto
} from '../models/safer.models';

@Injectable({
  providedIn: 'root'
})
export class SaferApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl =
    typeof window !== 'undefined' &&
    (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
      ? '/api'
      : 'https://safer-api.onrender.com/api';

  listSampleParcels(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/parcel/samples`);
  }

  loadSampleParcel(fileName: string): Observable<ParsedParcelDto> {
    return this.http.get<ParsedParcelDto>(`${this.baseUrl}/parcel/samples/${encodeURIComponent(fileName)}`);
  }

  uploadKmlFile(file: File): Observable<ParsedParcelDto> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ParsedParcelDto>(`${this.baseUrl}/parcel/parse-kml`, formData);
  }

  parseGeoJson(content: string, name?: string): Observable<ParsedParcelDto> {
    return this.http.post<ParsedParcelDto>(`${this.baseUrl}/parcel/parse-geojson`, { content, name });
  }

  getCrops(): Observable<CropDefinition[]> {
    return this.http.get<CropDefinition[]>(`${this.baseUrl}/rotation/crops`);
  }

  recommendRotations(request: RecommendationRequestDto): Observable<DecisionSupportResponseDto> {
    return this.http.post<DecisionSupportResponseDto>(`${this.baseUrl}/rotation/recommend`, request);
  }

  getAnnualPrecipitation(lat: number, lon: number, year: number): Observable<AnnualMonthlyProfileDto> {
    return this.http.get<AnnualMonthlyProfileDto>(
      `${this.baseUrl}/earthdata/precipitation/annual?lat=${lat}&lon=${lon}&year=${year}`
    );
  }

  getMonthlyDailyPrecipitation(lat: number, lon: number, year: number, month: number): Observable<MonthlyDailyPrecipitationDto> {
    return this.http.get<MonthlyDailyPrecipitationDto>(
      `${this.baseUrl}/earthdata/precipitation/daily?lat=${lat}&lon=${lon}&year=${year}&month=${month}`
    );
  }
}
