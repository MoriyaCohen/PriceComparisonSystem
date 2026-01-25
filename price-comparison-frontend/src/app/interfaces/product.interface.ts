export interface Product {
  storeId: string;
  itemCode: string;
  itemName: string;
  finalPrice: number;      // המחיר הקובע (להצגה ראשית)
  originalPrice: number;   // המחיר לפני הנחה (להצגה בקו חוצה)
  isPromoApplied: boolean; // האם יש מבצע?
  promoDescription: string | null;
  promoStartDate: string | null; // (ISO Date String)
  promoEndDate: string | null;   // (ISO Date String)
}