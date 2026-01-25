import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { LoginRequest, RegisterRequest } from '../../interfaces/auth.interfaces';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  loginForm: FormGroup;
  registerForm: FormGroup;
  
  isRegisterMode = signal(false);
  showPassword = signal(false);
  
  // תיקון מהצ'אט: הפיכת authService לפומבי
  constructor(
    public authService: AuthService,
    private router: Router,
    private fb: FormBuilder
  ) {
    // Redirect if already logged in
    if (this.authService.authState().isLoggedIn) {
      this.router.navigate(['/barcodeSearch']);
    }

    this.loginForm = this.fb.group({
      loginIdentifier: ['', [Validators.required, Validators.minLength(3)]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });

    this.registerForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.minLength(2)]],
      phone: ['', [Validators.pattern(/^[0-9\-\+\s\(\)]+$/)]],
      email: ['', [Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  // תיקון מהצ'אט: getter במקום property
  get authState() {
    return this.authService.authState;
  }

  onLoginSubmit(): void {
    if (this.loginForm.valid) {
      const request: LoginRequest = this.loginForm.value;
      
      console.log('🔐 Submitting login form for:', request.loginIdentifier);
      
      this.authService.login(request).subscribe({
        next: (response) => {
          console.log('📨 Login response in component:', response);
          if (response.success) {
            console.log('✅ Login successful, navigating to barcode search');
            // 🎯 תיקון: ניווט למסך חיפוש ברקוד במקום dashboard
            this.router.navigate(['/barcodeSearch']).then(() => {
              console.log('📍 נווט בהצלחה למסך חיפוש ברקוד');
            });
          }
        },
        error: (error) => {
          console.error('❌ Login error in component:', error);
        }
      });
    } else {
      console.log('❌ Login form is invalid');
      this.markFormGroupTouched(this.loginForm);
    }
  }

  onRegisterSubmit(): void {
    if (this.registerForm.valid) {
      const formValue = this.registerForm.value;
      
      // בדיקה שיש לפחות טלפון או אימייל
      if (!formValue.phone && !formValue.email) {
        this.authService.updateAuthState({ 
          error: 'נדרש לפחות מספר טלפון או כתובת אימייל' 
        });
        return;
      }
      
      const request: RegisterRequest = formValue;
      
      console.log('📝 Submitting registration form for:', request.fullName);
      
      this.authService.register(request).subscribe({
        next: (response) => {
          console.log('📨 Registration response in component:', response);
          if (response.success) {
            alert('רישום בוצע בהצלחה! עכשיו תוכל להתחבר');
            this.switchToLogin();
            // מילוי אוטומטי של הטופס עם הפרטים החדשים
            this.loginForm.patchValue({
              loginIdentifier: request.phone || request.email
            });
          }
        },
        error: (error) => {
          console.error('❌ Registration error in component:', error);
        }
      });
    } else {
      console.log('❌ Registration form is invalid');
      this.markFormGroupTouched(this.registerForm);
    }
  }

  switchToRegister(): void {
    this.isRegisterMode.set(true);
    this.authService.clearError();
    console.log('🔄 Switched to register mode');
  }

  switchToLogin(): void {
    this.isRegisterMode.set(false);
    this.authService.clearError();
    console.log('🔄 Switched to login mode');
  }

  togglePasswordVisibility(): void {
    this.showPassword.update(current => !current);
  }

  // בדיקת חיבור לשרת (לצורכי דיבוג)
  testServerConnection(): void {
    this.authService.testConnection().subscribe({
      next: (response) => {
        alert('✅ חיבור לשרת תקין: ' + JSON.stringify(response));
      },
      error: (error) => {
        alert('❌ שגיאה בחיבור לשרת: ' + error.message);
      }
    });
  }

  // עזר לטיפול בשגיאות טפסים
  getFieldError(form: FormGroup, fieldName: string): string {
    const field = form.get(fieldName);
    if (field?.errors && field.touched) {
      if (field.errors['required']) return `${fieldName} נדרש`;
      if (field.errors['minlength']) {
        const requiredLength = field.errors['minlength'].requiredLength;
        return `${fieldName} חייב להכיל לפחות ${requiredLength} תווים`;
      }
      if (field.errors['email']) return 'כתובת אימייל לא תקינה';
      if (field.errors['pattern']) return 'פורמט לא תקין';
    }
    return '';
  }

  isFieldInvalid(form: FormGroup, fieldName: string): boolean {
    const field = form.get(fieldName);
    return !!(field?.errors && field.touched);
  }

  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(key => {
      const control = formGroup.get(key);
      control?.markAsTouched();
    });
  }
}