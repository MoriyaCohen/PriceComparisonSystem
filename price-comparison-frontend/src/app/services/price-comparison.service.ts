  // src/app/shared/services/price-comparison.service.ts

  import { Injectable } from '@angular/core';
  import { HttpClient, HttpErrorResponse } from '@angular/common/http';
  import { Observable, throwError, BehaviorSubject } from 'rxjs';
  import { catchError, map, tap } from 'rxjs/operators';
  import { 
    PriceComparisonRequest,
    PriceComparisonResponse,
    ProductPriceInfo,
    SearchStatus,
    SortOption
  } from '../interfaces/price-comparison.interface';

  /**
   * שירות לטיפול בהשוואת מחירים
   * מנהל את הקשר עם השרת, מיון תוצאות ושמירת מצב החיפוש
   */
  @Injectable({
    providedIn: 'root'
  })
  export class PriceComparisonService {
    /** URL בסיס לשרת */
    // private readonly apiUrl = `${environment.apiUrl}/api/price-comparison`;
  private readonly apiUrl = 'http://localhost:5162/api/pricecomparison';
    /** מצב החיפוש הנוכחי */
    private searchStatusSubject = new BehaviorSubject<SearchStatus>(SearchStatus.IDLE);
    public searchStatus$ = this.searchStatusSubject.asObservable();

    /** תוצאות החיפוש האחרונות */
    private lastResultsSubject = new BehaviorSubject<PriceComparisonResponse | null>(null);
    public lastResults$ = this.lastResultsSubject.asObservable();

    /** אפשרות המיון הנוכחית */
    private currentSortOption = SortOption.PRICE_ASC;

    constructor(private http: HttpClient) {}

    /**
     * חיפוש מוצר לפי ברקוד והשוואת מחירים
     * @param barcode ברקוד המוצר
     * @returns Observable עם תוצאות החיפוש
     */
    searchProductByBarcode(barcode: string): Observable<PriceComparisonResponse> {
      console.log(`[PriceComparisonService] מתחיל חיפוש מוצר עבור ברקוד: ${barcode}`);
      
      // עדכון מצב החיפוש
      this.searchStatusSubject.next(SearchStatus.SEARCHING);
      
      const request: PriceComparisonRequest = { barcode };

      return this.http.post<PriceComparisonResponse>(`${this.apiUrl}/search`, request)
        .pipe(
          map(response => this.processSearchResponse(response)),
          tap(response => {
            console.log(`[PriceComparisonService] תוצאות חיפוש:`, response);
            
            // עדכון מצב החיפוש בהתאם לתוצאות
            if (response.success && response.priceDetails.length > 0) {
              this.searchStatusSubject.next(SearchStatus.SUCCESS);
            } else if (response.success && response.priceDetails.length === 0) {
              this.searchStatusSubject.next(SearchStatus.NOT_FOUND);
            } else {
              this.searchStatusSubject.next(SearchStatus.ERROR);
            }
            
            // שמירת התוצאות
            this.lastResultsSubject.next(response);
          }),
          catchError((error: HttpErrorResponse) => {
            console.error(`[PriceComparisonService] שגיאה בחיפוש:`, error);
            this.searchStatusSubject.next(SearchStatus.ERROR);
            return this.handleSearchError(error);
          })
        );
    }

    /**
     * עיבוד תוצאות החיפוש - מיון ועיבוד נתונים
     */
    private processSearchResponse(response: PriceComparisonResponse): PriceComparisonResponse {
      if (!response.success || !response.priceDetails) {
        return response;
      }

      // מיון התוצאות לפי המיון הנוכחי
      const sortedPrices = this.sortPriceDetails(response.priceDetails, this.currentSortOption);

      return {
        ...response,
        priceDetails: sortedPrices
      };
    }

    /**
     * מיון רשימת המחירים לפי האפשרות שנבחרה
     */
    sortPriceDetails(prices: ProductPriceInfo[], sortOption: SortOption): ProductPriceInfo[] {
      const sortedPrices = [...prices]; // יצירת עותק כדי לא לשנות את המקורי

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

      // שמירת התגובה עם השגיאה
      this.lastResultsSubject.next(errorResponse);

      return throwError(() => errorResponse);
    }
  }