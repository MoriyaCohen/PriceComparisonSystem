import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { XmlUploadComponent } from './components/xml-upload/xml-upload.component';
import { BarcodeSearchComponent } from './components/barcode-search/barcode-search.component';
import { BusStopSearchComponent } from './components/bus-stop-search/bus-stop-search.component';
import { SmartShoppingComponent } from './components/smart-shopping/smart-shopping.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/barcodeSearch', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { 
    path: 'dashboard', 
    component: XmlUploadComponent,
    canActivate: [authGuard]
  },
  { 
    path: 'barcodeSearch', 
    component: BarcodeSearchComponent,
    canActivate: [authGuard]
  },
  { 
    path: 'busStopSearch', 
    component: BusStopSearchComponent,
    canActivate: [authGuard]
  },
  {
    path: 'smart-shopping',
    loadComponent: () => import('./components/smart-shopping/smart-shopping.component').then(m => m.SmartShoppingComponent),
    canActivate: [authGuard]
  },

  { path: '**', redirectTo: '/barcodeSearch' }
];