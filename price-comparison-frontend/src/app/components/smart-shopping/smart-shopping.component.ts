import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { SmartShoppingService, SmartShoppingResult } from '../../services/smart-shopping.service';
import { HttpClientModule } from '@angular/common/http';

@Component({
  selector: 'app-smart-shopping',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, HttpClientModule],
  templateUrl: './smart-shopping.component.html',
  styleUrls: ['./smart-shopping.component.scss'],
  providers: [SmartShoppingService] // Provide service here if not provided in root or needed locally for standalone
})
export class SmartShoppingComponent {
  searchForm: FormGroup;
  results: SmartShoppingResult[] = [];
  isLoading = false;
  hasSearched = false;
  errorMessage: string | null = null;

  constructor(
    private fb: FormBuilder,
    private smartShoppingService: SmartShoppingService
  ) {
    this.searchForm = this.fb.group({
      stopId: ['', [Validators.required, Validators.pattern(/^[0-9]+$/)]], // Assuming numeric, string stop ID
      radiusKm: [1, [Validators.required, Validators.min(0.1)]],
      barcode: ['', [Validators.required, Validators.pattern(/^[0-9]+$/)]],
      showClub: [false],
      showQuantity: [false]
    });
  }

  onSubmit() {
    if (this.searchForm.invalid) {
      this.searchForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.hasSearched = false;
    this.errorMessage = null;
    this.results = [];

    const { stopId, radiusKm, barcode, showClub, showQuantity } = this.searchForm.value;

    this.smartShoppingService.getCheapestByStop(stopId, radiusKm, barcode, showClub, showQuantity)
      .subscribe({
        next: (data) => {
          this.results = data;
          this.isLoading = false;
          this.hasSearched = true;
        },
        error: (error) => {
          // console.error('Error fetching prices:', error);
          this.errorMessage = 'Could not fetch prices. Please try again later or check your inputs.';
          this.isLoading = false;
          this.hasSearched = true;
        }
      });
  }
}
