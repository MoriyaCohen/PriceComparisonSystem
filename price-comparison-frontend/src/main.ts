import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, Routes } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { AppComponent } from './app/app.component';

// 🔧 תיקון: הגדרה מפורשת של Routes עם טיפוסים נכונים
const routes: Routes = [
  { 
    path: '', 
    redirectTo: '/login', 
    pathMatch: 'full' as const  // 👈 תיקון: מפורש כ-const
  },
  { 
    path: 'login', 
    loadComponent: () => import('./app/components/login/login.component')
      .then(c => c.LoginComponent) 
  },
  { 
    path: 'barcodeSearch', 
    loadComponent: () => import('./app/components/barcode-search/barcode-search.component')
      .then(c => c.BarcodeSearchComponent) 
  },
  { 
    path: 'dashboard', 
    loadComponent: () => import('./app/components/xml-upload/xml-upload.component')
      .then(c => c.XmlUploadComponent) 
  },
  { 
    path: '**', 
    redirectTo: '/login' 
  }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),
    provideHttpClient(),
  ]
}).catch(err => console.error('Error starting application:', err));