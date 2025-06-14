import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { XmlUploadComponent } from './components/xml-upload/xml-upload.component';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: XmlUploadComponent },
  { path: '**', redirectTo: '/login' }
];