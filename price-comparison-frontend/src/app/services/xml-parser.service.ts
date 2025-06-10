import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { delay, catchError, tap } from 'rxjs/operators';
import { ParsedXMLData, ProductItem, StoreInfo, PriceSummary } from '../interfaces/xml-data.interfaces';

@Injectable({
  providedIn: 'root'
})
export class XmlParserService {

  constructor(private http: HttpClient) { }

  /**
   * Parse XML file content
   */
  parseXMLFile(file: File): Promise<ParsedXMLData> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      
      reader.onload = (e) => {
        try {
          const xmlContent = e.target?.result as string;
          const parsedData = this.parseXMLContent(xmlContent);
          resolve(parsedData);
        } catch (error) {
          reject(error);
        }
      };

      reader.onerror = () => {
        reject(new Error('שגיאה בקריאת הקובץ'));
      };

      reader.readAsText(file, 'UTF-8');
    });
  }

  /**
   * Parse XML content string
   */
  private parseXMLContent(xmlContent: string): ParsedXMLData {
    const parser = new DOMParser();
    const doc = parser.parseFromString(xmlContent, 'text/xml');

    // Check for parsing errors
    const parserError = doc.querySelector('parsererror');
    if (parserError) {
      throw new Error('קובץ XML לא תקין');
    }

    const root = doc.querySelector('Root');
    if (!root) {
      throw new Error('מבנה XML לא תקין - חסר Root element');
    }

    // Extract store info
    const storeInfo: StoreInfo = {
      chainId: this.getTextContent(root, 'ChainId'),
      subChainId: this.getTextContent(root, 'SubChainId'),
      storeId: this.getTextContent(root, 'StoreId'),
      bikoretNo: this.getTextContent(root, 'BikoretNo')
    };

    // Extract items
    const items = this.extractItems(root);

    if (items.length === 0) {
      throw new Error('לא נמצאו מוצרים בקובץ');
    }

    return {
      storeInfo,
      items,
      totalItems: items.length,
      uploadDate: new Date()
    };
  }

  /**
   * Extract items from XML
   */
  private extractItems(root: Element): ProductItem[] {
    const itemElements = root.querySelectorAll('Items > Item');
    const items: ProductItem[] = [];

    itemElements.forEach(itemEl => {
      // Skip empty items
      if (!itemEl.querySelector('ItemCode')) return;

      const item: ProductItem = {
        itemCode: this.getTextContent(itemEl, 'ItemCode'),
        itemName: this.getTextContent(itemEl, 'ItemNm'),
        manufacturerName: this.getTextContent(itemEl, 'ManufacturerName'),
        manufacturerCountry: this.getTextContent(itemEl, 'ManufactureCountry'),
        unitQty: this.getTextContent(itemEl, 'UnitQty'),
        quantity: parseFloat(this.getTextContent(itemEl, 'Quantity')) || 0,
        unitOfMeasure: this.getTextContent(itemEl, 'UnitOfMeasure'),
        isWeighted: this.getTextContent(itemEl, 'bIsWeighted') === '1',
        qtyInPackage: parseFloat(this.getTextContent(itemEl, 'QtyInPackage')) || 0,
        itemPrice: parseFloat(this.getTextContent(itemEl, 'ItemPrice')) || 0,
        unitOfMeasurePrice: parseFloat(this.getTextContent(itemEl, 'UnitOfMeasurePrice')) || 0,
        allowDiscount: this.getTextContent(itemEl, 'AllowDiscount') === '1',
        itemStatus: parseInt(this.getTextContent(itemEl, 'ItemStatus')) || 1,
        priceUpdateDate: this.getTextContent(itemEl, 'PriceUpdateDate')
      };

      items.push(item);
    });

    return items;
  }

  /**
   * Helper method to get text content
   */
  private getTextContent(parent: Element, tagName: string): string {
    const element = parent.querySelector(tagName);
    return element?.textContent?.trim() || '';
  }

  /**
   * Calculate price summary statistics
   */
  calculatePriceSummary(items: ProductItem[]): PriceSummary {
    if (items.length === 0) {
      return {
        totalItems: 0,
        averagePrice: 0,
        weightedItemsCount: 0,
        minPrice: 0,
        maxPrice: 0,
        totalValue: 0
      };
    }

    const prices = items.map(item => item.itemPrice);
    const totalValue = prices.reduce((sum, price) => sum + price, 0);
    const weightedItemsCount = items.filter(item => item.isWeighted).length;

    return {
      totalItems: items.length,
      averagePrice: totalValue / items.length,
      weightedItemsCount,
      minPrice: Math.min(...prices),
      maxPrice: Math.max(...prices),
      totalValue
    };
  }

  /**
   * Validate file before processing
   */
  validateFile(file: File): { isValid: boolean; error?: string } {
    // Check file type
    if (!file.name.toLowerCase().endsWith('.xml')) {
      return { isValid: false, error: 'אנא בחר קובץ XML בלבד' };
    }

    // Check file size (max 10MB)
    if (file.size > 10 * 1024 * 1024) {
      return { isValid: false, error: 'גודל הקובץ חורג מ-10MB' };
    }

    // Check if file is empty
    if (file.size === 0) {
      return { isValid: false, error: 'הקובץ ריק' };
    }

    return { isValid: true };
  }

  /**
   * Save data to backend - המתודה החשובה!
   */
  saveToDatabase(data: ParsedXMLData): Observable<any> {
    console.log('Sending data to backend:', data);
    
    const payload = {
      storeInfo: data.storeInfo,
      items: data.items,
      totalItems: data.totalItems
    };

    return this.http.post('http://localhost:5161/api/xmlprocessing/upload-from-frontend', payload)
      .pipe(
        tap(response => console.log('Backend response:', response)),
        catchError(error => {
          console.error('Error details:', error);
          return throwError(() => new Error(
            error.error?.message || 'שגיאה בשמירת הנתונים'
          ));
        })
      );
  }
}