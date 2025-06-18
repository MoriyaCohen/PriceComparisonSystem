// src/app/components/barcode-search.component.ts

import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil, debounceTime, distinctUntilChanged, Observable } from 'rxjs';
import { BarcodeValidationService } from '../../services/barcode-validation.service';
import { PriceComparisonService } from '../../services/price-comparison.service';
import { barcodeValidator } from '../../validators/barcode-validator.directive';
import { BarcodeValidationStatus } from '../../interfaces/barcode-validation.interface';
import { SearchStatus } from '../../interfaces/price-comparison.interface';
import { CommonModule } from '@angular/common';


/**
 * קומפוננטה ראשית לחיפוש ברקוד והשוואת מחירים
 * כוללת טופס לקליטת ברקוד, בדיקת תקינות והצגת תוצאות
 */
@Component({
  selector: 'app-barcode-search',
  standalone: true,
  templateUrl: './barcode-search.component.html',
  imports: [ReactiveFormsModule, CommonModule],
  styleUrls: ['./barcode-search.component.scss']
})
export class BarcodeSearchComponent implements OnInit, OnDestroy {
  /** טופס לקליטת הברקוד */
  barcodeForm: FormGroup;

  /** מצבי הקומפוננטה - לחיבור עם הטמפלט */
  BarcodeValidationStatus = BarcodeValidationStatus;
  SearchStatus = SearchStatus;

  /** מצב בדיקת הברקוד הנוכחי */
  validationStatus = BarcodeValidationStatus.NOT_VALIDATED;

  /** מצב החיפוש הנוכחי */
  searchStatus$!: Observable<SearchStatus>;

  /** תוצאות החיפוש האחרונות */
  searchResults$!: Observable<any>;

  /** הודעת שגיאה נוכחית */
  currentErrorMessage = '';

  /** האם הטופס נשלח */
  isFormSubmitted = false;

  /** Subject לניהול הרשמות ל-Observables */
  private destroy$ = new Subject<void>();

  constructor(
    private formBuilder: FormBuilder,
    private barcodeValidationService: BarcodeValidationService,
    private priceComparisonService: PriceComparisonService
  ) {
    this.barcodeForm = this.createBarcodeForm();
    
    // הגדרת ה-Observables כאן לאחר שה-services זמינים
    this.searchStatus$ = this.priceComparisonService.searchStatus$;
    this.searchResults$ = this.priceComparisonService.lastResults$;
  }

  ngOnInit(): void {
    console.log('[BarcodeSearchComponent] התחלת טעינת קומפוננטה');
    
    // הגדרת מאזינים לשינויים בשדה הברקוד
    this.setupBarcodeFieldListeners();
    
    // איפוס מצב החיפוש כשנכנסים לקומפוננטה
    this.priceComparisonService.resetSearch();
  }

  ngOnDestroy(): void {
    console.log('[BarcodeSearchComponent] השמדת קומפוננטה');
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * יצירת טופס הברקוד עם ולידציות
   */
  private createBarcodeForm(): FormGroup {
    return this.formBuilder.group({
      barcode: ['', [
        Validators.required,
        barcodeValidator() // הולידטור המותאם שיצרנו
      ]]
    });
  }

  /**
   * הגדרת מאזינים לשינויים בשדה הברקוד
   * בדיקה בזמן אמת בזמן הקלדה
   */
  private setupBarcodeFieldListeners(): void {
    const barcodeControl = this.barcodeForm.get('barcode');
    
    if (barcodeControl) {
      barcodeControl.valueChanges
        .pipe(
          takeUntil(this.destroy$),
          debounceTime(300), // המתנה של 300ms לאחר הפסקת הקלדה
          distinctUntilChanged() // רק אם הערך באמת השתנה
        )
        .subscribe(value => {
          console.log('[BarcodeSearchComponent] שינוי בשדה ברקוד:', value);
          this.onBarcodeValueChange(value);
        });
    }
  }

  /**
   * טיפול בשינוי ערך הברקוד
   * בדיקה מהירה אם הפורמט נראה תקין
   */
  private onBarcodeValueChange(value: string): void {
    // איפוס הודעות שגיאה קודמות
    this.currentErrorMessage = '';
    
    if (!value || value.trim() === '') {
      this.validationStatus = BarcodeValidationStatus.NOT_VALIDATED;
      return;
    }

    // בדיקה מהירה של הפורמט
    const isValidFormat = this.barcodeValidationService.isValidBarcodeFormat(value);
    
    if (isValidFormat) {
      this.validationStatus = BarcodeValidationStatus.VALID;
    } else {
      this.validationStatus = BarcodeValidationStatus.INVALID;
    }
  }

  /**
   * שליחת הטופס וביצוע חיפוש
   */
  onSubmit(): void {
    console.log('[BarcodeSearchComponent] שליחת טופס');
    
    this.isFormSubmitted = true;
    
    // בדיקת תקינות הטופס
    if (this.barcodeForm.invalid) {
      console.log('[BarcodeSearchComponent] טופס לא תקין:', this.barcodeForm.errors);
      this.markFormGroupTouched();
      return;
    }

    const barcodeValue = this.barcodeForm.get('barcode')?.value?.trim();
    if (!barcodeValue) {
      return;
    }

    // ניקוי הברקוד
    const cleanedBarcode = this.barcodeValidationService.cleanBarcode(barcodeValue);
    console.log('[BarcodeSearchComponent] ברקוד מנוקה:', cleanedBarcode);

    // תחילת תהליך הבדיקה והחיפוש
    this.searchProduct(cleanedBarcode);
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
   * ביצוע השוואת מחירים לאחר בדיקת הברקוד
   */
  private performPriceComparison(barcode: string): void {
    console.log('[BarcodeSearchComponent] מתחיל השוואת מחירים עבור:', barcode);

    this.priceComparisonService.searchProductByBarcode(barcode)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (results) => {
          console.log('[BarcodeSearchComponent] תוצאות השוואת מחירים:', results);
          // התוצאות נשמרות אוטומטית בשירות ומוצגות דרך searchResults$
        },
        error: (error) => {
          console.error('[BarcodeSearchComponent] שגיאה בהשוואת מחירים:', error);
          this.currentErrorMessage = error.errorMessage || 'שגיאה בחיפוש המוצר';
        }
      });
  }

  /**
   * איפוס הטופס והתוצאות
   */
  resetForm(): void {
    console.log('[BarcodeSearchComponent] איפוס טופס');
    
    this.barcodeForm.reset();
    this.isFormSubmitted = false;
    this.validationStatus = BarcodeValidationStatus.NOT_VALIDATED;
    this.currentErrorMessage = '';
    this.priceComparisonService.resetSearch();
  }

  /**
   * סימון כל השדות בטופס כ-touched (להצגת שגיאות)
   */
  private markFormGroupTouched(): void {
    Object.keys(this.barcodeForm.controls).forEach(key => {
      const control = this.barcodeForm.get(key);
      control?.markAsTouched();
    });
  }

  /**
   * בדיקה אם שדה ספציפי מציג שגיאה
   */
  shouldShowFieldError(fieldName: string): boolean {
    const field = this.barcodeForm.get(fieldName);
    return !!(field && field.invalid && (field.dirty || field.touched || this.isFormSubmitted));
  }

  /**
   * קבלת הודעת השגיאה של שדה ספציפי
   */
  getFieldErrorMessage(fieldName: string): string {
    const field = this.barcodeForm.get(fieldName);
    
    if (!field || !field.errors) {
      return '';
    }

    // טיפול בסוגי שגיאות שונים
    if (field.errors['required']) {
      return 'שדה חובה';
    }
    
    if (field.errors['barcodeInvalid']) {
      return field.errors['barcodeInvalid'].message;
    }
    
    if (field.errors['barcodeInvalidLength']) {
      return field.errors['barcodeInvalidLength'].message;
    }
    
    if (field.errors['barcodeInvalidChecksum']) {
      return field.errors['barcodeInvalidChecksum'].message;
    }

    return 'שגיאה לא ידועה';
  }

  /**
   * האם הטופס במצב טעינה
   */
  get isLoading(): boolean {
    return this.validationStatus === BarcodeValidationStatus.VALIDATING;
  }

  /**
   * קבלת כתובת ערך הברקוד הנוכחי
   */
  get currentBarcodeValue(): string {
    return this.barcodeForm.get('barcode')?.value || '';
  }
}