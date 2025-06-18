// src/app/shared/services/barcode-validation.service.ts

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { 
  BarcodeValidationRequest, 
  BarcodeValidationResponse,
  BarcodeValidationError
} from '../interfaces/barcode-validation.interface';
import { isLikelyValidBarcode, normalizeBarcode } from '../validators/barcode-validator.directive';

/**
 * שירות לטיפול בבדיקת תקינות ברקודים
 * כולל בדיקות צד לקוח וקשר עם השרת
 */
@Injectable({
  providedIn: 'root'
})
export class BarcodeValidationService {
  /** URL בסיס לשרת */
  // private readonly apiUrl = `${environment.apiUrl}/api/barcode`;
 private readonly apiUrl = 'http://localhost:5162/api/barcode';
  constructor(private http: HttpClient) {}

  /**
   * בדיקת תקינות ברקוד - תחילה צד לקוח, אחר כך שרת
   * @param barcode הברקוד לבדיקה
   * @returns Observable עם תוצאת הבדיקה
   */
  validateBarcode(barcode: string): Observable<BarcodeValidationResponse> {
    console.log(`[BarcodeValidationService] מתחיל בדיקת ברקוד: ${barcode}`);
    
    // שלב 1: בדיקה צד לקוח
    const clientValidation = this.validateBarcodeClientSide(barcode);
    if (!clientValidation.isValid) {
      console.log(`[BarcodeValidationService] בדיקת צד לקוח נכשלה:`, clientValidation);
      return of(clientValidation);
    }

    // שלב 2: בדיקה בשרת
    return this.validateBarcodeServerSide(clientValidation.normalizedBarcode!);
  }

  /**
   * בדיקת תקינות בצד הלקוח
   * מהירה ולא דורשת קשר לשרת
   */
  private validateBarcodeClientSide(barcode: string): BarcodeValidationResponse {
    // בדיקה בסיסית - האם יש ברקוד
    if (!barcode || barcode.trim() === '') {
      return {
        isValid: false,
        errorMessage: 'אנא הזן מספר ברקוד'
      };
    }

    // ניקוי הברקוד
    const normalized = normalizeBarcode(barcode);
    
    // בדיקה אם נראה כמו ברקוד תקין
    if (!isLikelyValidBarcode(normalized)) {
      const validLengths = [8, 12, 13];
      return {
        isValid: false,
        errorMessage: `ברקוד חייב להכיל ${validLengths.join(' או ')} ספרות בלבד. הברקוד שהוזן: "${normalized}" (${normalized.length} ספרות)`
      };
    }

    // אם הגענו לכאן - נראה תקין בצד הלקוח
    return {
      isValid: true,
      normalizedBarcode: normalized
    };
  }

  /**
   * בדיקת תקינות בשרת
   * כולל בדיקת ספרת ביקורת ובדיקה אם המוצר קיים במערכת
   */
  private validateBarcodeServerSide(barcode: string): Observable<BarcodeValidationResponse> {
    const request: BarcodeValidationRequest = { barcode };
    
    console.log(`[BarcodeValidationService] שולח לשרת לבדיקה:`, request);

    return this.http.post<BarcodeValidationResponse>(`${this.apiUrl}/validate`, request)
      .pipe(
        tap(response => console.log(`[BarcodeValidationService] תגובה מהשרת:`, response)),
        catchError((error: HttpErrorResponse) => {
          console.error(`[BarcodeValidationService] שגיאה בקשר לשרת:`, error);
          return this.handleValidationError(error);
        })
      );
  }

  /**
   * טיפול בשגיאות קשר לשרת
   */
  private handleValidationError(error: HttpErrorResponse): Observable<BarcodeValidationResponse> {
    let errorMessage = 'שגיאה לא ידועה בבדיקת הברקוד';

    if (error.error instanceof ErrorEvent) {
      // שגיאת רשת או צד לקוח
      errorMessage = 'בעיה בחיבור לשרת. אנא בדוק את החיבור לאינטרנט';
    } else {
      // שגיאה מהשרת
      switch (error.status) {
        case 400:
          errorMessage = error.error?.message || 'נתונים לא תקינים';
          break;
        case 404:
          errorMessage = 'שירות בדיקת ברקוד לא נמצא';
          break;
        case 500:
          errorMessage = 'שגיאה פנימית בשרת';
          break;
        default:
          errorMessage = `שגיאה בשרת (קוד ${error.status})`;
      }
    }

    const response: BarcodeValidationResponse = {
      isValid: false,
      errorMessage: errorMessage
    };

    return of(response);
  }

  /**
   * בדיקה מהירה אם ברקוד נראה תקין (ללא קשר לשרת)
   * שימושית לבדיקות בזמן אמת בזמן הקלדה
   */
  isValidBarcodeFormat(barcode: string): boolean {
    return isLikelyValidBarcode(barcode);
  }

  /**
   * ניקוי ברקוד מתווים לא רצויים
   */
  cleanBarcode(barcode: string): string {
    return normalizeBarcode(barcode);
  }
}