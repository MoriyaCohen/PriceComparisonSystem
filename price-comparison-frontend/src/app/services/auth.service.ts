import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { tap } from 'rxjs/operators';
import { 
  LoginRequest, 
  RegisterRequest, 
  LoginResponse, 
  AuthResult, 
  AuthState, 
  UserInfo 
} from '../interfaces/auth.interfaces';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  // פורט 5161 כפי שנקבע בצ'אט
  private readonly API_URL = 'http://localhost:5161/api/auth';
  private readonly USER_KEY = 'user_info';

  authState = signal<AuthState>({
    isLoggedIn: false,
    isLoading: false,
    error: null,
    user: null
  });

  constructor(private http: HttpClient) {
    this.initializeAuthState();
  }

  /**
   * אתחול מצב Authentication מLocalStorage
   * נקרא בעת טעינת האפליקציה
   */
  private initializeAuthState(): void {
    try {
      const userStr = localStorage.getItem(this.USER_KEY);
      
      if (userStr) {
        const user = JSON.parse(userStr);
        this.authState.set({
          isLoggedIn: true,
          isLoading: false,
          error: null,
          user: user
        });
        console.log('✅ Auth state initialized from localStorage');
      } else {
        console.log('ℹ️ No saved auth state found');
      }
    } catch (error) {
      console.error('❌ Error initializing auth state:', error);
      this.logout(); // מחיקת נתונים פגומים
    }
  }

  /**
   * התחברות משתמש
   * זרימה: UI Form → Service → HTTP → Backend → Token Storage → State Update
   */
  login(request: LoginRequest): Observable<LoginResponse> {
    console.log('🔐 Starting login process for:', request.loginIdentifier);
    
    this.updateAuthState({ isLoading: true, error: null });

    return this.http.post<LoginResponse>(`${this.API_URL}/login`, request)
      .pipe(
        tap(response => {
          console.log('📨 Login response received:', { 
            success: response.success, 
            message: response.message,
            hasUser: !!response.user 
          });
          
          if (response.success && response.user) {
            // שמירה ב-localStorage
            localStorage.setItem(this.USER_KEY, JSON.stringify(response.user));
            
            // עדכון State
            this.authState.set({
              isLoggedIn: true,
              isLoading: false,
              error: null,
              user: response.user
            });
            
            console.log('✅ Login successful, user logged in:', response.user.fullName);
          } else {
            this.updateAuthState({ 
              isLoading: false, 
              error: response.message 
            });
            console.log('❌ Login failed:', response.message);
          }
        }),
        catchError(this.handleError.bind(this))
      );
  }

  /**
   * רישום משתמש חדש
   */
  register(request: RegisterRequest): Observable<AuthResult> {
    console.log('📝 Starting registration process for:', request.fullName);
    
    this.updateAuthState({ isLoading: true, error: null });

    return this.http.post<AuthResult>(`${this.API_URL}/register`, request)
      .pipe(
        tap(response => {
          console.log('📨 Registration response received:', { 
            success: response.success, 
            message: response.message 
          });
          
          this.updateAuthState({ 
            isLoading: false, 
            error: response.success ? null : response.message 
          });
        }),
        catchError(this.handleError.bind(this))
      );
  }

  /**
   * יציאה מהמערכת
   * זרימה: UI → Service → Clear Storage → State Reset
   */
  logout(): void {
    console.log('🚪 Logging out user');
    
    // מחיקת נתונים מLocalStorage
    localStorage.removeItem(this.USER_KEY);
    
    // איפוס State
    this.authState.set({
      isLoggedIn: false,
      isLoading: false,
      error: null,
      user: null
    });
    
    console.log('✅ User logged out successfully');
  }

  /**
   * קבלת Token נוכחי (לעתיד אם נעבור ל-JWT)
   */
  getToken(): string | null {
    return localStorage.getItem(this.USER_KEY);
  }

  /**
   * בדיקה האם המשתמש מחובר
   */
  isLoggedIn(): boolean {
    return this.authState().isLoggedIn;
  }

  /**
   * קבלת פרטי המשתמש הנוכחי
   */
  getCurrentUser(): UserInfo | null {
    return this.authState().user;
  }

  /**
   * עדכון חלקי של Auth State
   * פומבי כדי שקומפוננטים יוכלו להשתמש בו (תיקון מהצ'אט)
   */
  public updateAuthState(updates: Partial<AuthState>): void {
    this.authState.update(current => ({ ...current, ...updates }));
  }

  /**
   * מחיקת הודעת שגיאה
   */
  clearError(): void {
    this.updateAuthState({ error: null });
  }

  /**
   * טיפול בשגיאות HTTP (מתוקן)
   */
  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('❌ HTTP Error occurred:', {
      status: error.status,
      statusText: error.statusText,
      url: error.url,
      message: error.message
    });
    
    let errorMessage = 'שגיאה בתקשורת עם השרת';
    
    // טיפול מפורט בסוגי שגיאות
    if (error.error && error.error.message) {
      errorMessage = error.error.message;
    } else if (error.status === 0) {
      errorMessage = 'לא ניתן להתחבר לשרת. בדוק שהשרת רץ על פורט 5161';
    } else if (error.status === 400) {
      errorMessage = 'פרטי התחברות שגויים';
    } else if (error.status === 404) {
      errorMessage = 'שירות לא נמצא. בדוק כתובת השרת';
    } else if (error.status >= 500) {
      errorMessage = 'שגיאה בשרת. נסה שוב מאוחר יותר';
    }
    
    this.updateAuthState({ 
      isLoading: false, 
      error: errorMessage 
    });
    
    return throwError(() => new Error(errorMessage));
  }

  /**
   * בדיקת חיבור לשרת (לצורכי דיבוג)
   */
  testConnection(): Observable<any> {
    console.log('🧪 Testing connection to server...');
    return this.http.get(`${this.API_URL}/test`)
      .pipe(
        tap(response => console.log('✅ Server connection OK:', response)),
        catchError(error => {
          console.error('❌ Server connection failed:', error);
          return throwError(() => error);
        })
      );
  }
}