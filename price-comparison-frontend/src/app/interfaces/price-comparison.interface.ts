// interfaces/price-comparison.interfaces.ts

export interface PriceComparisonResponse {
  success: boolean;
  errorMessage?: string;
  productInfo?: ProductInfo;
  priceDetails: ProductPriceInfo[];
  statistics?: PriceStatistics;
}

export interface ProductInfo {
  productName: string;
  barcode: string;
  manufacturerName?: string;
}

export interface ProductPriceInfo {
  productId: number;
  productName: string;
  chainName: string;
  storeName: string;
  storeAddress?: string;
  currentPrice: number;
  unitPrice?: number;
  unitOfMeasure?: string;
  isWeighted: boolean;
  allowDiscount: boolean;
  lastUpdated: Date;
  isMinPrice?: boolean;
  subChainName?: string; 
}

export interface PriceStatistics {
  minPrice: number;
  maxPrice: number;
  averagePrice: number;
  chainCount: number;
  storeCount: number;
  totalResults: number;
}

export interface LocalDataStatus {
  loadedChains: number;
  loadedStores: number;
  totalProducts: number;
  lastRefresh: Date;
  isDataAvailable: boolean;
  statusMessage: string;
}

export enum SearchStatus {
  IDLE = 'idle',
  SEARCHING = 'searching',
  SUCCESS = 'success',
  NOT_FOUND = 'not_found',
  ERROR = 'error'
}

export enum SortOption {
  PRICE_ASC = 'price_asc',
  PRICE_DESC = 'price_desc',
  CHAIN_NAME = 'chain_name',
  STORE_NAME = 'store_name'
}

export interface BarcodeSearchRequest {
  barcode: string;
}

export interface BarcodeValidationResponse {
  isValid: boolean;
  errorMessage?: string;
  normalizedBarcode?: string;
}

export interface CheapestPriceResponse {
  storeId: string;
  itemCode: string;
  itemName: string;
  finalPrice: number;
  originalPrice?: number;
  isPromoApplied: boolean;
  promoDescription?: string;
  promoStartDate?: string;
  promoEndDate?: string;
}

export interface Product {
  chainId: string;
  storeId: string;
  storeName?: string;
  storeAddress?: string;
  itemCode: string;
  itemName: string;
  regularPrice: number;
  promoPrice?: number;
  hasPromo: boolean;
  promoDescription?: string;
  promoEndDate?: string;
  isClubMemberPromo: boolean;
  isCreditCardPromo: boolean;
  finalCalculatedPrice: number;
  priceTypeLabel: string;
}

export interface StoreNearStop {
  storeName: string;
  city: string;
  address: string;
  distanceKm: number;
  mapLink: string;
}
