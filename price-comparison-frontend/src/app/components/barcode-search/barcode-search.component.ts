import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Observable, Subject } from 'rxjs';
import { PriceComparisonService } from '../../services/price-comparison.service';
import { Product } from '../../interfaces/price-comparison.interface';

@Component({
  selector: 'app-barcode-search',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './barcode-search.component.html',
  styleUrls: ['./barcode-search.component.scss']
})
export class BarcodeSearchComponent implements OnInit, OnDestroy {
  barcodeForm: FormGroup;

  // Checkbox state
  isClub: boolean = false;
  isQuantity: boolean = false;

  // Results
  products$: Observable<Product[]> | null = null;

  isLoading: boolean = false;
  errorMessage: string = '';

  private destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private priceComparisonService: PriceComparisonService
  ) {
    this.barcodeForm = this.createForm();
  }

  ngOnInit(): void {}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private createForm(): FormGroup {
    return this.fb.group({
      barcode: ['', [
        Validators.required,
        Validators.pattern(/^\d{8,13}$/),
        Validators.minLength(8),
        Validators.maxLength(13)
      ]]
    });
  }

  onSubmit(): void {
    if (this.barcodeForm.invalid) {
      this.errorMessage = 'אנא הזן ברקוד תקין (8-13 ספרות)';
      return;
    }

    const barcode = this.barcodeForm.get('barcode')?.value?.trim();
    if (!barcode) return;

    this.searchProduct(barcode);
  }

  searchProduct(barcode: string): void {
    this.errorMessage = '';
    this.isLoading = true;
    this.barcodeForm.disable(); // Disable form while loading

    this.products$ = this.priceComparisonService.getCheapestSmart(barcode, this.isClub, this.isQuantity);

    this.products$.subscribe({
      next: (products) => {
        this.isLoading = false;
        this.barcodeForm.enable(); 

        if (!products || products.length === 0) {
          this.errorMessage = 'לא נמצאו תוצאות לברקוד זה';
        } else {
          console.log('Products with real store info:', products);
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.barcodeForm.enable();
        console.error('Error fetching products:', err);
        this.errorMessage = 'המוצר לא נמצא בחנויות המסונכרנות';
      }
    });
  }

  onFilterChange(): void {
    const barcode = this.barcodeForm.get('barcode')?.value?.trim();
    if (barcode && this.barcodeForm.valid) {
      this.searchProduct(barcode);
    }
  }

  resetForm(): void {
    this.barcodeForm.reset();
    this.products$ = null;
    this.errorMessage = '';
    this.isClub = false;
    this.isQuantity = false;
  }
}
