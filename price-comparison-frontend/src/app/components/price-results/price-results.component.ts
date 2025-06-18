import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProductPriceInfo, SortOption } from '../../interfaces/price-comparison.interface';

@Component({
  selector: 'app-price-results',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './price-results.component.html',
  styleUrls: ['./price-results.component.scss']
})
export class PriceResultsComponent implements OnInit, OnChanges {

  @Input() priceDetails: ProductPriceInfo[] = [];
  @Input() minPrice?: number;

  filteredResults: ProductPriceInfo[] = [];

  // הגדרת אפשרויות מיון עם תוויות
  sortOptions = [
    { value: SortOption.PRICE_ASC, label: 'מהזול ליקר' },
    { value: SortOption.PRICE_DESC, label: 'מהיקר לזול' },
    { value: SortOption.CHAIN_NAME, label: 'לפי רשת' },
    { value: SortOption.STORE_NAME, label: 'לפי סניף' }
  ];

  currentSort: SortOption = SortOption.PRICE_ASC;
  selectedChain = 'all';
  showOnlyMinPrice = false;

  ngOnInit(): void {
    this.updateResults();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['priceDetails'] || changes['minPrice']) {
      this.markMinPrices();
      this.updateResults();
    }
  }

  private markMinPrices(): void {
    if (!this.priceDetails || this.priceDetails.length === 0) return;
    const min = this.minPrice ?? Math.min(...this.priceDetails.map(p => p.currentPrice));
    this.priceDetails.forEach(p => (p as any).isMinPrice = p.currentPrice === min);
  }

  updateResults(): void {
    let results = [...this.priceDetails];

    if (this.selectedChain !== 'all') {
      results = results.filter(p => p.chainName === this.selectedChain);
    }

    if (this.showOnlyMinPrice) {
      results = results.filter(p => (p as any).isMinPrice);
    }

    this.filteredResults = this.sortResults(results, this.currentSort);
  }

  private sortResults(results: ProductPriceInfo[], sortBy: SortOption): ProductPriceInfo[] {
    return results.sort((a, b) => {
      switch (sortBy) {
        case SortOption.PRICE_ASC:
          return a.currentPrice - b.currentPrice;
        case SortOption.PRICE_DESC:
          return b.currentPrice - a.currentPrice;
        case SortOption.CHAIN_NAME:
          return a.chainName.localeCompare(b.chainName, 'he');
        case SortOption.STORE_NAME:
          return a.storeName.localeCompare(b.storeName, 'he');
        default:
          return 0;
      }
    });
  }

  get availableChains(): string[] {
    return Array.from(new Set(this.priceDetails.map(p => p.chainName))).sort();
  }

  onSortChange(sort: string): void {
    this.currentSort = sort as SortOption;
    this.updateResults();
  }

  onChainFilter(chainName: string): void {
    this.selectedChain = chainName;
    this.updateResults();
  }

  toggleMinPriceOnly(): void {
    this.showOnlyMinPrice = !this.showOnlyMinPrice;
    this.updateResults();
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('he-IL', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  calculateSavings(currentPrice: number): number {
    return this.minPrice && currentPrice > this.minPrice ? currentPrice - this.minPrice : 0;
  }

  trackByStore(index: number, item: ProductPriceInfo): string {
    return `${item.chainName}-${item.storeName}-${item.currentPrice}`;
  }
}
