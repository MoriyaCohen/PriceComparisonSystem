// src/app/shared/interfaces/barcode-validation.interface.ts

/**
 * ממשק לבקשת בדיקת ברקוד - הנתונים שנשלחים לשרת
 */
export interface BarcodeValidationRequest {
  /** מספר הברקוד שהמשתמש הזין */
  barcode: string;
}

/**
 * ממשק לתגובה על בדיקת תקינות ברקוד מהשרת
 */
export interface BarcodeValidationResponse {
  /** האם הברקוד תקין מבחינה טכנית */
  isValid: boolean;
  /** הודעת שגיאה במקרה שהברקוד לא תקין */
  errorMessage?: string;
  /** הברקוד המנורמל (לאחר ניקוי ופורמט) */
  normalizedBarcode?: string;
}

/**
 * מצבי בדיקת ברקוד - enum לניהול מצבי הבדיקה השונים
 */
export enum BarcodeValidationStatus {
  /** לא בוצעה בדיקה עדיין */
  NOT_VALIDATED = 'not_validated',
  /** בתהליך בדיקה */
  VALIDATING = 'validating',
  /** ברקוד תקין */
  VALID = 'valid',
  /** ברקוד לא תקין */
  INVALID = 'invalid'
}

/**
 * פרטי שגיאות בדיקת ברקוד - לפירוט מדויק של הבעיה
 */
export interface BarcodeValidationError {
  /** קוד השגיאה */
  errorCode: string;
  /** הודעת השגיאה למשתמש */
  message: string;
  /** פרטים נוספים לפיתוח/דיבאג */
  details?: string;
}