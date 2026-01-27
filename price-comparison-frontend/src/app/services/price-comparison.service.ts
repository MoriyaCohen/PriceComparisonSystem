import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { tap, map } from 'rxjs/operators';
import { Product, PriceComparisonResponse, StoreNearStop } from '../interfaces/price-comparison.interface';

@Injectable({
  providedIn: 'root'
})
export class PriceComparisonService {
  private apiUrl = 'http://localhost:5162/api/PriceComparison';

  private CHAIN_MAPPING: { [key: string]: string } = {
    '7290058197699': 'ויקטורי',
    '7290058140886': 'רמי לוי',
    '7290027600007': 'שופרסל',
    '7290103152017': 'אושר עד',
    '7290803800003': 'יוחננוף',
    '7290633800006': 'חצי חינם',
    '7290873255550': 'טיב טעם',
    '7290058173198': 'יינות ביתן',
    '7290058108879': 'קינג סטור',
    '7290058156016': 'סופר ספיר',
    '7290058148776': 'שוק העיר',
    '7290058145478': 'מעיין 2000',
    '7290058159628': 'מרכז המזון',
    '7290661400001': 'מחסני השוק',
    '7290058160839': 'קואופ שופ'
  };

  constructor(private http: HttpClient) { }

  getCheapestSmart(barcode: string, isClub: boolean = false, isQuantity: boolean = false): Observable<Product[]> {
    // URL matches the backend controller: [HttpGet("get-cheapest-smart/{barcode}")]
    const url = `${this.apiUrl}/get-cheapest-smart/${barcode}`;
    
    let params = new HttpParams()
      .set('showClub', isClub.toString())
      .set('showQuantity', isQuantity.toString());

    console.log(`[PriceComparisonService] Calling GET ${url} with params:`, params.toString());

    return this.http.get<any[]>(url, { params }).pipe(
      map(response => {
        if (!response || !Array.isArray(response)) {
          console.warn('[PriceComparisonService] Invalid response format:', response);
          return [];
        }
        
        return response.map(item => {
          // IMPORTANT: Do NOT use item.storeLabel as it contains the address and causes duplication
          let storeName = item.storeName || item.StoreName; 
          const storeId = item.storeId || item.StoreId;
          const address = item.storeAddress || item.StoreAddress || "";
          let city = item.storeCity || item.StoreCity;
          
          // Resolve Chain Name from ID if needed
          let chainName = item.chainId;
          if (chainName && /^\d+$/.test(chainName.toString().trim())) {
             if (this.CHAIN_MAPPING[chainName]) {
               chainName = this.CHAIN_MAPPING[chainName];
             }
          }

          // 1. Try to extract city from address if missing
          if (!city && address.includes(',')) {
             const parts = address.split(',');
             city = parts[parts.length - 1].trim();
          }

          // 2. Fix numeric store names (e.g. "970")
          if (!storeName || /^\d+$/.test(storeName.toString().trim())) {
            if (city) {
              storeName = city;
            } else {
              storeName = `סניף ${storeId}`;
            }
          }

          // 3. Ensure Chain Name is part of the Store Name
          // If storeName is "Ramat Gan", we want "Victory Ramat Gan"
          if (chainName && storeName && !storeName.includes(chainName)) {
             // Only prepend if chainName is NOT numeric (to avoid "7290... Jerusalem")
             if (!/^\d+$/.test(chainName.toString())) {
                 storeName = `${chainName} ${storeName}`;
             }
          }

          return {
            chainId: chainName,
            storeId: item.storeId,
            storeName: storeName, 
            storeAddress: address,
            storeCity: city,
            itemCode: item.itemCode,
            itemName: item.itemName,
            regularPrice: item.regularPrice,
            finalCalculatedPrice: item.finalCalculatedPrice,
            priceTypeLabel: item.priceTypeLabel,
            hasPromo: item.hasPromo,
            promoPrice: item.promoPrice,
            promoDescription: item.promoDescription,
            promoEndDate: item.promoEndDate,
            isClubMemberPromo: item.isClubMemberPromo,
            isCreditCardPromo: item.isCreditCardPromo
          } as Product;
        });
      }),
      tap({
        next: (data) => console.log(`[PriceComparisonService] Success: ${data.length} products found`),
        error: (error) => console.error(`[PriceComparisonService] Error:`, error)
      })
    );
  }

  findStoresByBusStop(stopId: string, radiusKm: number): Observable<StoreNearStop[]> {
    const url = `http://localhost:5162/api/BusStopSearch/FindStores`;
    let params = new HttpParams()
      .set('stopId', stopId)
      .set('radiusKm', radiusKm.toString());

    return this.http.get<StoreNearStop[]>(url, { params });
  }
}
