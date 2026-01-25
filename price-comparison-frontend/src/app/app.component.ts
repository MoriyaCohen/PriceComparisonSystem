import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule],
  template: `
    <div class="app-container">
      <nav class="main-nav" *ngIf="authService.authState().isLoggedIn">
        <div class="nav-brand">השוואת מחירים 🛒</div>
        <div class="nav-links">
          <a routerLink="/barcodeSearch" routerLinkActive="active">חיפוש ברקוד</a>
          <a routerLink="/busStopSearch" routerLinkActive="active">חיפוש לפי תחנה</a>
          <a routerLink="/smart-shopping" routerLinkActive="active">חיפוש חכם</a>
        </div>
        <div class="user-info">
          שלום, {{ authService.authState().user?.fullName }}
          <button (click)="logout()" class="logout-btn">התנתק</button>
        </div>
      </nav>
      <main class="content">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  title = 'מערכת השוואת מחירים';
  authService = inject(AuthService);

  logout() {
    this.authService.logout();
  }
}