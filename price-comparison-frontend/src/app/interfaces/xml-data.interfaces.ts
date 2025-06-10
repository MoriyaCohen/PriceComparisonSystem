// interfaces/xml-data.interfaces.ts

export interface StoreInfo {
  chainId: string;
  subChainId: string;
  storeId: string;
  bikoretNo: string;
}

export interface ProductItem {
  itemCode: string;
  itemName: string;
  manufacturerName: string;
  manufacturerCountry: string;
  unitQty: string;
  quantity: number;
  unitOfMeasure: string;
  isWeighted: boolean;
  qtyInPackage: number;
  itemPrice: number;
  unitOfMeasurePrice: number;
  allowDiscount: boolean;
  itemStatus: number;
  priceUpdateDate: string;
}

export interface ParsedXMLData {
  storeInfo: StoreInfo;
  items: ProductItem[];
  totalItems: number;
  uploadDate: Date;
}

export interface PriceSummary {
  totalItems: number;
  averagePrice: number;
  weightedItemsCount: number;
  minPrice: number;
  maxPrice: number;
  totalValue: number;
}

export interface UploadState {
  isLoading: boolean;
  isDragOver: boolean;
  isSaving: boolean;
  error: string | null;
  success: boolean;
}