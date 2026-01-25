import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { PriceComparisonService } from '../../services/price-comparison.service';
import { StoreNearStop } from '../../interfaces/price-comparison.interface';

@Component({
  selector: 'app-bus-stop-search',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './bus-stop-search.component.html',
  styleUrls: ['./bus-stop-search.component.scss']
})
export class BusStopSearchComponent {
  searchForm: FormGroup;
  stores: StoreNearStop[] = [];
  isLoading: boolean = false;
  errorMessage: string = '';
  hasSearched: boolean = false;

  constructor(
    private fb: FormBuilder,
    private priceComparisonService: PriceComparisonService
  ) {
    this.searchForm = this.fb.group({
      stopId: ['', [Validators.required]],
      radiusKm: [10, [Validators.required, Validators.min(1)]]
    });
  }

  onSubmit(): void {
    if (this.searchForm.invalid) {
      return;
    }

    const { stopId, radiusKm } = this.searchForm.value;
    this.searchStores(stopId, radiusKm);
  }

  searchStores(stopId: string, radiusKm: number): void {
    this.errorMessage = '';
    this.isLoading = true;
    this.hasSearched = true;
    this.stores = [];
    this.searchForm.disable();

    this.priceComparisonService.findStoresByBusStop(stopId, radiusKm).subscribe({
      next: (stores) => {
        this.isLoading = false;
        this.searchForm.enable();
        
        if (!stores || stores.length === 0) {
          this.errorMessage = 'לא נמצאו סופרים ברדיוס שהוגדר';
        } else {
          this.stores = stores;
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.searchForm.enable();
        console.error('Error fetching stores:', err);
        
        if (err.status === 404) {
             // 404 could mean the station wasn't found OR the endpoint doesn't exist
             this.errorMessage = 'התחנה לא נמצאה במערכת';
        } else if (err.status === 0) {
             this.errorMessage = 'לא ניתן להתחבר לשרת. וודא שהשרת פעיל.';
        } else {
             // Show more specific error if available
             const serverMsg = err.error?.message || err.error || '';
             this.errorMessage = `שגיאה בחיבור לשרת (${err.status})${serverMsg ? ': ' + serverMsg : ''}`;
        }
      }
    });
  }
}
