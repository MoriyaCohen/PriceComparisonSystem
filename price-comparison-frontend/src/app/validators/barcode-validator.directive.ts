// src/app/shared/validators/barcode.validator.ts

import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * ולידטור מותאם אישית לבדיקת תקינות ברקוד
 * הולך ליצור פונקציה שמחזירה ולידטור עבור Angular Forms
 */
export function barcodeValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    
    // אם השדה ריק - לא נבדוק (נניח שיש ולידטור required נפרד)
    if (!value) {
      return null;
    }

    const barcode = value.toString().trim();
    
    // בדיקה בסיסית: האם מכיל רק ספרות
    if (!/^\d+$/.test(barcode)) {
      return { 
        barcodeInvalid: { 
          message: 'ברקוד חייב להכיל ספרות בלבד',
          actualValue: barcode 
        } 
      };
    }

    // בדיקת אורך: ברקוד תקין הוא 8, 12 או 13 ספרות
    const validLengths = [8, 12, 13];
    if (!validLengths.includes(barcode.length)) {
      return { 
        barcodeInvalidLength: { 
          message: `ברקוד חייב להיות באורך 8, 12 או 13 ספרות. האורך הנוכחי: ${barcode.length}`,
          actualLength: barcode.length,
          expectedLengths: validLengths
        } 
      };
    }

    // בדיקת ספרת ביקורת (checksum) לברקודים מסוג EAN
    if (!isValidChecksum(barcode)) {
      return { 
        barcodeInvalidChecksum: { 
          message: 'ספרת הביקורת של הברקוד אינה תקינה',
          barcode: barcode 
        } 
      };
    }

    // אם הגענו לכאן - הברקוד תקין
    return null;
  };
}

/**
 * פונקציה לבדיקת ספרת ביקורת של ברקוד EAN-13/EAN-8
 * אלגוריתם סטנדרטי: סכום משוקלל של הספרות
 */
function isValidChecksum(barcode: string): boolean {
  // המרה למערך של מספרים
  const digits = barcode.split('').map(digit => parseInt(digit, 10));
  
  if (barcode.length === 13) {
    // EAN-13: ספרות במיקום זוגי (מתחילים מ-0) מוכפלות ב-1, 
    // ספרות במיקום אי-זוגי מוכפלות ב-3
    let sum = 0;
    for (let i = 0; i < 12; i++) {
      const multiplier = (i % 2 === 0) ? 1 : 3;
      sum += digits[i] * multiplier;
    }
    
    // ספרת הביקורת היא המספר שצריך להוסיף כדי שהסכום יהיה מתחלק ב-10
    const checkDigit = (10 - (sum % 10)) % 10;
    return checkDigit === digits[12];
    
  } else if (barcode.length === 8) {
    // EAN-8: אותו עקרון אבל עם 7 ספרות + ספרת ביקורת
    let sum = 0;
    for (let i = 0; i < 7; i++) {
      const multiplier = (i % 2 === 0) ? 1 : 3;
      sum += digits[i] * multiplier;
    }
    
    const checkDigit = (10 - (sum % 10)) % 10;
    return checkDigit === digits[7];
    
  } else if (barcode.length === 12) {
    // UPC-A: כמו EAN-13 אבל עם 12 ספרות
    let sum = 0;
    for (let i = 0; i < 11; i++) {
      const multiplier = (i % 2 === 0) ? 1 : 3;
      sum += digits[i] * multiplier;
    }
    
    const checkDigit = (10 - (sum % 10)) % 10;
    return checkDigit === digits[11];
  }
  
  return false;
}

/**
 * פונקציה עזר לניקוי וטיפול בברקוד
 * מסירה רווחים, מקפים וכו'
 */
export function normalizeBarcode(barcode: string): string {
  if (!barcode) return '';
  
  // הסרת כל התווים שאינם ספרות
  return barcode.replace(/\D/g, '');
}

/**
 * פונקציה לבדיקה מהירה אם ברקוד נראה תקין (ללא ולידציה מלאה)
 * שימושית לבדיקות מהירות בזמן הקלדה
 */
export function isLikelyValidBarcode(barcode: string): boolean {
  if (!barcode) return false;
  
  const normalized = normalizeBarcode(barcode);
  const validLengths = [8, 12, 13];
  
  return validLengths.includes(normalized.length) && /^\d+$/.test(normalized);
}
