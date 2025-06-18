// src/app/shared/interfaces/price-comparison.interface.ts

/**
 * ממשק לבקשת השוואת מחירים
 */
export interface PriceComparisonRequest {
  /** ברקוד המוצר לחיפוש */
  barcode: string;
}

/**
 * מידע על מוצר וסניף ספציפי
 */
export interface ProductPriceInfo {
  /** מזהה המוצר */
  productId: number;
  /** שם המוצר */
  productName: string;
  /** שם הרשת */
  chainName: string;
  /** שם הסניף */
  storeName: string;
  /** כתובת הסניף */
  storeAddress?: string;
  /** מחיר נוכחי */
  currentPrice: number;
  /** מחיר ליחידה (אם רלוונטי) */
  unitPrice?: number;
  /** יחידת מידה */
  unitOfMeasure?: string;
  /** האם מוצר שקיל */
  isWeighted: boolean;
  /** האם מותר הנחה */
  allowDiscount: boolean;
  /** תאריך עדכון אחרון */
  lastUpdated: Date;
    isMinPrice?: boolean;

}

/**
 * סטטיסטיקות מחירים
 */
export interface PriceStatistics {
  /** מחיר זול ביותר */
  minPrice: number;
  /** מחיר יקר ביותר */
  maxPrice: number;
  /** מחיר ממוצע */
  averagePrice: number;
  /** כמות סניפים שנמצאו */
  storeCount: number;
  /** כמות רשתות שנמצאו */
  chainCount: number;
}

/**
 * תגובת השוואת מחירים מהשרת
 */
export interface PriceComparisonResponse {
  /** האם החיפוש היה מוצלח */
  success: boolean;
  /** הודעת שגיאה במקרה של כישלון */
  errorMessage?: string;
  /** פרטי המוצר שנמצא */
  productInfo?: {
    productName: string;
    barcode: string;
    manufacturerName?: string;
  };
  /** סטטיסטיקות המחירים */
  statistics?: PriceStatistics;
  /** רשימת כל המחירים שנמצאו, ממוינת לפי מחיר */
  priceDetails: ProductPriceInfo[];
}

/**
 * מצבי חיפוש מוצר
 */
export enum SearchStatus {
  /** לא בוצע חיפוש */
  IDLE = 'idle',
  /** בתהליך חיפוש */
  SEARCHING = 'searching',
  /** נמצאו תוצאות */
  SUCCESS = 'success',
  /** לא נמצאו תוצאות */
  NOT_FOUND = 'not_found',
  /** שגיאה בחיפוש */
  ERROR = 'error'
}

/**
 * אפשרויות מיון תוצאות
 */
export enum SortOption {
  /** מיון לפי מחיר - מהזול ליקר */
  PRICE_ASC = 'price_asc',
  /** מיון לפי מחיר - מהיקר לזול */
  PRICE_DESC = 'price_desc',
  /** מיון לפי שם רשת */
  CHAIN_NAME = 'chain_name',
  /** מיון לפי שם סניף */
  STORE_NAME = 'store_name'
  
}
