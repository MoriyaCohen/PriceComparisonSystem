// xml-upload.component.ts
import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { XmlParserService } from '../../services/xml-parser.service';
import { ParsedXMLData, PriceSummary, UploadState } from '../../interfaces/xml-data.interfaces';
import { Router } from '@angular/router';

@Component({
  selector: 'app-xml-upload',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './xml-upload.component.html',
  styleUrls: ['./xml-upload.component.scss']
})
export class XmlUploadComponent {
  
  // State signals
  uploadState = signal<UploadState>({
    isLoading: false,
    isDragOver: false,
    isSaving: false,
    error: null,
    success: false
  });

  parsedData = signal<ParsedXMLData | null>(null);
  priceSummary = signal<PriceSummary | null>(null);

  constructor(private xmlParserService: XmlParserService,  private router: Router,
) {}

  // Drag & Drop handlers
  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.updateUploadState({ isDragOver: true });
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.updateUploadState({ isDragOver: false });
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.updateUploadState({ isDragOver: false });
    
    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.processFile(files[0]);
    }
  }

  // File selection handler
  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.processFile(input.files[0]);
    }
  }

  // Process the selected file
  async processFile(file: File) {
    // Reset state
    this.resetState();

    // Validate file
    const validation = this.xmlParserService.validateFile(file);
    if (!validation.isValid) {
      this.updateUploadState({ error: validation.error! });
      return;
    }

    this.updateUploadState({ isLoading: true });

    try {
      const parsedData = await this.xmlParserService.parseXMLFile(file);
      const summary = this.xmlParserService.calculatePriceSummary(parsedData.items);
      
      this.parsedData.set(parsedData);
      this.priceSummary.set(summary);
      this.updateUploadState({ isLoading: false, success: true });
      
    } catch (error) {
      this.updateUploadState({ 
        isLoading: false, 
        error: 'שגיאה בפרסור קובץ ה-XML: ' + (error as Error).message 
      });
    }
  }

  // Save data to database
  saveData() {
    const data = this.parsedData();
    if (!data) return;

    this.updateUploadState({ isSaving: true });
    
    this.xmlParserService.saveToDatabase(data).subscribe({
      next: (response) => {
        this.updateUploadState({ isSaving: false });
        if (response.success) {
          alert(response.message);
          // Optionally reset or navigate
        }
      },
      error: (error) => {
        this.updateUploadState({ 
          isSaving: false, 
          error: 'שגיאה בשמירת הנתונים: ' + error.message 
        });
      }
    });
  }

  // Reset all state
  resetUpload() {
    this.resetState();
    this.parsedData.set(null);
    this.priceSummary.set(null);
  }
  barcode(){
     this.router.navigate(['/barcodeSearch']);
  }

  // Helper methods
  private resetState() {
    this.updateUploadState({
      isLoading: false,
      isDragOver: false,
      isSaving: false,
      error: null,
      success: false
    });
  }

  private updateUploadState(updates: Partial<UploadState>) {
    this.uploadState.update(current => ({ ...current, ...updates }));
  }

  // Getters for template
  get currentState() {
    return this.uploadState();
  }

  get hasData() {
    return this.parsedData() !== null;
  }

  get summary() {
    return this.priceSummary();
  }
}