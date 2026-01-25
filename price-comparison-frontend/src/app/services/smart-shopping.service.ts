import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SmartShoppingResult {
  storeName: string;
  storeAddress: string;
  originalPrice: number; // For strikethrough logic (if promo exists)
  finalPrice: number;    // The highlighted price
  hasPromo: boolean;     // Helper to determine if we should show original price
  chainName?: string;
  distanceKm?: number;
}

@Injectable({
  providedIn: 'root'
})
export class SmartShoppingService {
  // Correctly pointing to port 5162 as requested
  private apiUrl = 'http://localhost:5162/api/SmartShopping';

  constructor(private http: HttpClient) {}

  getCheapestByStop(
    stopId: string,
    radiusKm: number,
    barcode: string,
    showClub: boolean = false,
    showQuantity: boolean = false
  ): Observable<SmartShoppingResult[]> {
    let params = new HttpParams()
      .set('stopId', stopId)
      .set('radiusKm', radiusKm.toString())
      .set('barcode', barcode)
      .set('showClub', showClub.toString())
      .set('showQuantity', showQuantity.toString());

    return this.http.get<SmartShoppingResult[]>(`${this.apiUrl}/GetCheapestByStop`, { params });
  }
}
