import { ProductPriceInfo } from '../../interfaces/price-comparison.interface';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Observable, Subject, takeUntil } from 'rxjs';
import { PriceComparisonService } from '../../services/price-comparison.service';
import { BarcodeValidationService } from '../../services/barcode-validation.service';
import { 
  PriceComparisonResponse, 
  SearchStatus, 
  LocalDataStatus 
} from '../../interfaces/price-comparison.interface';
import { BarcodeValidationStatus } from '../../interfaces/barcode-validation.interface';

@Component({
  selector: 'app-barcode-search',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './barcode-search.component.html',
  styleUrls: ['./barcode-search.component.scss']
})
export class BarcodeSearchComponent implements OnInit, OnDestroy {
  barcodeForm: FormGroup;
  searchResults$: Observable<PriceComparisonResponse | null>;
  searchStatus$: Observable<SearchStatus>;
  localDataStatus: LocalDataStatus | null = null;
  
  currentErrorMessage: string = '';
  isLoading: boolean = false;
  useLocalSearch: boolean = true; // ברירת מחדל לחיפוש מקומי
  validationStatus: BarcodeValidationStatus = BarcodeValidationStatus.NOT_VALIDATED;
  
  // Enum references for template
  SearchStatus = SearchStatus;
  BarcodeValidationStatus = BarcodeValidationStatus;
  
  private destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private priceComparisonService: PriceComparisonService,
    private barcodeValidationService: BarcodeValidationService
  ) {
    this.barcodeForm = this.createForm();
    this.searchResults$ = this.priceComparisonService.lastResults$;
    this.searchStatus$ = this.priceComparisonService.searchStatus$;
  }

  ngOnInit(): void {
    console.log('[BarcodeSearchComponent] רכיב הופעל');
    
    // מעקב אחר מצב החיפוש
    this.searchStatus$
      .pipe(takeUntil(this.destroy$))
      .subscribe((status: SearchStatus) => {
        this.isLoading = status === SearchStatus.SEARCHING;
        console.log(`[BarcodeSearchComponent] מצב חיפוש: ${status}`);
      });

    // טעינת מצב נתונים מקומיים
    this.loadLocalDataStatus();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * יצירת טופס החיפוש
   */
  private createForm(): FormGroup {
    return this.fb.group({
      barcode: ['', [
        Validators.required,
        Validators.pattern(/^\d{8,13}$/),
        Validators.minLength(8),
        Validators.maxLength(13)
      ]]
    });
  }

  /**
   * טיפול בהגשת הטופס - קריאה מה-template
   */
  onSubmit(): void {
    this.onSearchSubmit();
  }

  /**
   * חיפוש מוצר לפי ברקוד
   */
  onSearchSubmit(): void {
    if (this.barcodeForm.invalid) {
      this.markFormGroupTouched();
      this.currentErrorMessage = 'אנא הזן ברקוד תקין (8-13 ספרות)';
      return;
    }

    const barcode = this.barcodeForm.get('barcode')?.value?.trim();
    if (!barcode) {
      this.currentErrorMessage = 'אנא הזן ברקוד';
      return;
    }

    this.currentErrorMessage = '';
    this.searchProduct(barcode);
  }

  /**
   * ביצוע החיפוש - בדיקת ברקוד + השוואת מחירים
   */
  private searchProduct(barcode: string): void {
    console.log('[BarcodeSearchComponent] מתחיל חיפוש עבור:', barcode);
    
    this.validationStatus = BarcodeValidationStatus.VALIDATING;
    this.currentErrorMessage = '';

    // שלב 1: בדיקת תקינות הברקוד
    this.barcodeValidationService.validateBarcode(barcode)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (validationResult) => {
          console.log('[BarcodeSearchComponent] תוצאת בדיקת ברקוד:', validationResult);
          
          if (validationResult.isValid) {
            this.validationStatus = BarcodeValidationStatus.VALID;
            // שלב 2: חיפוש מוצר
            this.performPriceComparison(validationResult.normalizedBarcode || barcode);
          } else {
            this.validationStatus = BarcodeValidationStatus.INVALID;
            this.currentErrorMessage = validationResult.errorMessage || 'ברקוד לא תקין';
          }
        },
        error: (error) => {
          console.error('[BarcodeSearchComponent] שגיאה בבדיקת ברקוד:', error);
          this.validationStatus = BarcodeValidationStatus.INVALID;
          this.currentErrorMessage = 'שגיאה בבדיקת הברקוד';
        }
      });
  }

  /**
   * ביצוע השוואת מחירים
   */
  private performPriceComparison(barcode: string): void {
    console.log(`[BarcodeSearchComponent] מבצע חיפוש מחירים עבור: ${barcode}`);

    // בחירת סוג החיפוש
    const searchObservable = this.useLocalSearch 
      ? this.priceComparisonService.searchByBarcodeLocal(barcode)
      : this.priceComparisonService.searchByBarcode(barcode);

    searchObservable
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: PriceComparisonResponse) => {
          console.log('[BarcodeSearchComponent] חיפוש הושלם:', response);
          if (!response.success) {
            this.currentErrorMessage = response.errorMessage || 'מוצר לא נמצא';
          }
        },
        error: (error: any) => {
          console.error('[BarcodeSearchComponent] שגיאה בחיפוש:', error);
          this.currentErrorMessage = error.errorMessage || 'שגיאה בחיפוש המוצר';
        }
      });
  }

  /**
   * איפוס הטופס
   */
  resetForm(): void {
    this.barcodeForm.reset();
    this.currentErrorMessage = '';
    this.validationStatus = BarcodeValidationStatus.NOT_VALIDATED;
    this.priceComparisonService.resetSearch();
    console.log('[BarcodeSearchComponent] טופס אופס');
  }

  /**
   * שינוי סוג החיפוש (מקומי vs מסד נתונים)
   */
  toggleSearchType(): void {
    this.useLocalSearch = !this.useLocalSearch;
    console.log(`[BarcodeSearchComponent] סוג חיפוש שונה ל: ${this.useLocalSearch ? 'מקומי' : 'מסד נתונים'}`);
    
    // איפוס תוצאות קיימות
    this.priceComparisonService.resetSearch();
  }

  /**
   * רענון נתונים מקומיים
   */
  refreshLocalData(): void {
    console.log('[BarcodeSearchComponent] מתחיל רענון נתונים מקומיים');
    
    this.priceComparisonService.refreshLocalData()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (success: boolean) => {
          if (success) {
            console.log('[BarcodeSearchComponent] רענון נתונים הצליח');
            this.loadLocalDataStatus();
          } else {
            console.warn('[BarcodeSearchComponent] רענון נתונים נכשל');
            this.currentErrorMessage = 'רענון נתונים נכשל';
          }
        },
        error: (error: any) => {
          console.error('[BarcodeSearchComponent] שגיאה ברענון נתונים:', error);
          this.currentErrorMessage = 'שגיאה ברענון נתונים';
        }
      });
  }

  /**
   * טעינת מצב נתונים מקומיים
   */
  private loadLocalDataStatus(): void {
    this.priceComparisonService.getLocalDataStatus()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (status: LocalDataStatus) => {
          this.localDataStatus = status;
          console.log('[BarcodeSearchComponent] מצב נתונים מקומיים:', status);
        },
        error: (error: any) => {
          console.error('[BarcodeSearchComponent] שגיאה בקבלת מצב נתונים:', error);
        }
      });
  }

  /**
   * סימון כל השדות כנגעו
   */
  private markFormGroupTouched(): void {
    Object.keys(this.barcodeForm.controls).forEach(key => {
      const control = this.barcodeForm.get(key);
      control?.markAsTouched();
    });
  }

  /**
   * בדיקה האם צריך להציג שגיאה לשדה
   */
  shouldShowFieldError(fieldName: string): boolean {
    const field = this.barcodeForm.get(fieldName);
    return !!(field && field.invalid && (field.dirty || field.touched));
  }

  /**
   * בדיקה האם שדה מסוים שגוי
   */
  isFieldInvalid(fieldName: string): boolean {
    const field = this.barcodeForm.get(fieldName);
    return !!(field && field.invalid && (field.dirty || field.touched));
  }

  /**
   * קבלת הודעת שגיאה לשדה
   */
  getFieldErrorMessage(fieldName: string): string {
    const field = this.barcodeForm.get(fieldName);
    
    if (field?.errors) {
      if (field.errors['required']) {
        return 'שדה זה הוא חובה';
      }
      if (field.errors['pattern'] || field.errors['minlength'] || field.errors['maxlength']) {
        return 'ברקוד חייב להכיל 8-13 ספרות בלבד';
      }
    }
    
    return '';
  }

  /**
   * פורמט תאריך עבור התצוגה
   */
  formatDate(date: Date): string {
    if (!date) return 'לא זמין';
    
    return new Date(date).toLocaleDateString('he-IL', {
      day: '2-digit',
      month: '2-digit', 
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
  
  getCheapestPrice(): ProductPriceInfo | null {
    let currentResults: PriceComparisonResponse | null = null;
    this.searchResults$.subscribe(res => currentResults = res).unsubscribe();
    
    if (!currentResults?.priceDetails || currentResults.priceDetails.length === 0) {
      return null;
    }
    
    return currentResults.priceDetails[0]; // הראשון הוא הזול ביותר
  }

  /**
   * מחזיר את שאר התוצאות (2-4)
   */
  getAdditionalResults(): ProductPriceInfo[] {
    let currentResults: PriceComparisonResponse | null = null;
    this.searchResults$.subscribe(res => currentResults = res).unsubscribe();
    
    if (!currentResults?.priceDetails || currentResults.priceDetails.length <= 1) {
      return [];
    }
    
    return currentResults.priceDetails.slice(1); // מהשני ואילך
  }

  /**
   * trackBy function לביצועים
   */
  trackByPrice(index: number, price: ProductPriceInfo): string {
    return `${price.chainName}-${price.storeName}-${price.currentPrice}`;
  }
}