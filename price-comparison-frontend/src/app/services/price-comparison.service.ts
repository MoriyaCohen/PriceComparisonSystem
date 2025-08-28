import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, BehaviorSubject, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { 
  PriceComparisonResponse, 
  ProductPriceInfo, 
  SearchStatus, 
  SortOption,
  LocalDataStatus 
} from '../interfaces/price-comparison.interface';

@Injectable({
  providedIn: 'root'
})
export class PriceComparisonService {
  private readonly apiUrl = 'http://localhost:5162/api/pricecomparison';
  
  // מצב החיפוש הנוכחי
  private searchStatusSubject = new BehaviorSubject<SearchStatus>(SearchStatus.IDLE);
  public searchStatus$ = this.searchStatusSubject.asObservable();
  
  // תוצאות החיפוש האחרונות
  private lastResultsSubject = new BehaviorSubject<PriceComparisonResponse | null>(null);
  public lastResults$ = this.lastResultsSubject.asObservable();
  
  // אפשרות מיון נוכחית
  private currentSortOption: SortOption = SortOption.PRICE_ASC;

  constructor(private http: HttpClient) {
    console.log('[PriceComparisonService] שירות הושק');
  }

  /**
   * חיפוש מוצר לפי ברקוד (חיפוש מקומי בקבצי XML)
   */
  searchByBarcodeLocal(barcode: string): Observable<PriceComparisonResponse> {
    console.log(`[PriceComparisonService] מתחיל חיפוש מקומי עבור ברקוד: ${barcode}`);
    
    // עדכון מצב לחיפוש
    this.searchStatusSubject.next(SearchStatus.SEARCHING);
    
    const requestBody = { barcode: barcode.trim() };
    
    return this.http.post<PriceComparisonResponse>(`${this.apiUrl}/search-local`, requestBody)
      .pipe(
        tap(response => {
          console.log(`[PriceComparisonService] תגובה מהשרת:`, response);
          
          if (response.success && response.priceDetails && response.priceDetails.length > 0) {
            // מיון התוצאות לפי העדפה נוכחית
            const sortedResponse = {
              ...response,
              priceDetails: this.sortPriceDetails(response.priceDetails, this.currentSortOption)
            };
            
            this.searchStatusSubject.next(SearchStatus.SUCCESS);
            this.lastResultsSubject.next(sortedResponse);
          } else {
            this.searchStatusSubject.next(SearchStatus.NOT_FOUND);
            this.lastResultsSubject.next(response);
          }
        }),
        catchError(error => this.handleSearchError(error))
      );
  }

  /**
   * חיפוש מוצר לפי ברקוד (מסד נתונים - הפונקציונליות הקיימת)
   */
  searchByBarcode(barcode: string): Observable<PriceComparisonResponse> {
    console.log(`[PriceComparisonService] מתחיל חיפוש במסד נתונים עבור ברקוד: ${barcode}`);
    
    // עדכון מצב לחיפוש
    this.searchStatusSubject.next(SearchStatus.SEARCHING);
    
    const requestBody = { barcode: barcode.trim() };
    
    return this.http.post<PriceComparisonResponse>(`${this.apiUrl}/search`, requestBody)
      .pipe(
        tap(response => {
          console.log(`[PriceComparisonService] תגובה מהשרת:`, response);
          
          if (response.success && response.priceDetails && response.priceDetails.length > 0) {
            // מיון התוצאות לפי העדפה נוכחית
            const sortedResponse = {
              ...response,
              priceDetails: this.sortPriceDetails(response.priceDetails, this.currentSortOption)
            };
            
            this.searchStatusSubject.next(SearchStatus.SUCCESS);
            this.lastResultsSubject.next(sortedResponse);
          } else {
            this.searchStatusSubject.next(SearchStatus.NOT_FOUND);
            this.lastResultsSubject.next(response);
          }
        }),
        catchError(error => this.handleSearchError(error))
      );
  }

  /**
   * קבלת מצב נתוני XML המקומיים
   */
  getLocalDataStatus(): Observable<LocalDataStatus> {
    return this.http.get<LocalDataStatus>(`${this.apiUrl}/local-data-status`)
      .pipe(
        catchError(error => {
          console.error('[PriceComparisonService] שגיאה בקבלת מצב נתונים מקומיים:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * רענון נתוני XML מקומיים
   */
  refreshLocalData(): Observable<boolean> {
    console.log('[PriceComparisonService] מתחיל רענון נתונים מקומיים');
    
    return this.http.post<boolean>(`${this.apiUrl}/refresh-local-data`, {})
      .pipe(
        tap(success => {
          if (success) {
            console.log('[PriceComparisonService] רענון נתונים מקומיים הצליח');
          } else {
            console.warn('[PriceComparisonService] רענון נתונים מקומיים נכשל');
          }
        }),
        catchError(error => {
          console.error('[PriceComparisonService] שגיאה ברענון נתונים מקומיים:', error);
          return throwError(() => error);
        })
      );
  }

  /**
   * מיון רשימת מחירים לפי אפשרות נתונה
   */
  private sortPriceDetails(priceDetails: ProductPriceInfo[], sortOption: SortOption): ProductPriceInfo[] {
    const sortedPrices = [...priceDetails];
    
    switch (sortOption) {
      case SortOption.PRICE_ASC:
        return sortedPrices.sort((a, b) => a.currentPrice - b.currentPrice);
        
      case SortOption.PRICE_DESC:
        return sortedPrices.sort((a, b) => b.currentPrice - a.currentPrice);
      
      case SortOption.CHAIN_NAME:
        return sortedPrices.sort((a, b) => a.chainName.localeCompare(b.chainName, 'he'));
      
      case SortOption.STORE_NAME:
        return sortedPrices.sort((a, b) => a.storeName.localeCompare(b.storeName, 'he'));
      
      default:
        return sortedPrices;
    }
  }

  /**
   * שינוי אפשרות המיון והחלה על התוצאות הקיימות
   */
  changeSortOption(sortOption: SortOption): void {
    console.log(`[PriceComparisonService] שינוי מיון ל: ${sortOption}`);
    
    this.currentSortOption = sortOption;
    
    // החלת המיון על התוצאות הקיימות
    const currentResults = this.lastResultsSubject.value;
    if (currentResults && currentResults.priceDetails) {
      const sortedPrices = this.sortPriceDetails(currentResults.priceDetails, sortOption);
      const updatedResults = {
        ...currentResults,
        priceDetails: sortedPrices
      };
      this.lastResultsSubject.next(updatedResults);
    }
  }

  /**
   * קבלת אפשרות המיון הנוכחית
   */
  getCurrentSortOption(): SortOption {
    return this.currentSortOption;
  }

  /**
   * איפוס מצב החיפוש
   */
  resetSearch(): void {
    console.log(`[PriceComparisonService] איפוס מצב החיפוש`);
    this.searchStatusSubject.next(SearchStatus.IDLE);
    this.lastResultsSubject.next(null);
  }

  /**
   * קבלת הסניף עם המחיר הזול ביותר
   */
  getCheapestStore(results: PriceComparisonResponse): ProductPriceInfo | null {
    if (!results.success || !results.priceDetails || results.priceDetails.length === 0) {
      return null;
    }

    return results.priceDetails.reduce((cheapest, current) => 
      current.currentPrice < cheapest.currentPrice ? current : cheapest
    );
  }

  /**
   * קבלת הסניף עם המחיר היקר ביותר
   */
  getMostExpensiveStore(results: PriceComparisonResponse): ProductPriceInfo | null {
    if (!results.success || !results.priceDetails || results.priceDetails.length === 0) {
      return null;
    }

    return results.priceDetails.reduce((expensive, current) => 
      current.currentPrice > expensive.currentPrice ? current : expensive
    );
  }

  /**
   * חישוב חיסכון אפשרי בין המחיר הזול והיקר
   */
  calculatePotentialSavings(results: PriceComparisonResponse): number {
    const cheapest = this.getCheapestStore(results);
    const mostExpensive = this.getMostExpensiveStore(results);
    
    if (!cheapest || !mostExpensive) {
      return 0;
    }

    return mostExpensive.currentPrice - cheapest.currentPrice;
  }

  /**
   * טיפול בשגיאות חיפוש
   */
  private handleSearchError(error: HttpErrorResponse): Observable<PriceComparisonResponse> {
    let errorMessage = 'שגיאה לא ידועה בחיפוש המוצר';

    if (error.error instanceof ErrorEvent) {
      // שגיאת רשת או צד לקוח
      errorMessage = 'בעיה בחיבור לשרת. אנא בדוק את החיבור לאינטרנט';
    } else {
      // שגיאה מהשרת
      switch (error.status) {
        case 400:
          errorMessage = error.error?.message || 'ברקוד לא תקין';
          break;
        case 404:
          errorMessage = 'מוצר לא נמצא במערכת';
          break;
        case 500:
          errorMessage = 'שגיאה פנימית בשרת';
          break;
        default:
          errorMessage = `שגיאה בשרת (קוד ${error.status})`;
      }
    }

    const errorResponse: PriceComparisonResponse = {
      success: false,
      errorMessage: errorMessage,
      priceDetails: []
    };

    // עדכון מצב החיפוש לשגיאה
    this.searchStatusSubject.next(SearchStatus.ERROR);
    
    // שמירת התגובה עם השגיאה
    this.lastResultsSubject.next(errorResponse);

    return throwError(() => errorResponse);
  }
}