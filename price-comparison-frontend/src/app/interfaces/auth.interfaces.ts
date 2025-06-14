/**
 * Interfaces לניהול Authentication ב-Frontend
 * מבטיחות Type Safety ועקביות בנתונים
 */

export interface LoginRequest {
  loginIdentifier: string; // טלפון או אימייל
  password: string;
}

export interface RegisterRequest {
  phone: string;
  email: string;
  fullName: string;
  password: string;
}

export interface LoginResponse {
  success: boolean;
  message: string;
  user?: UserInfo;
}

export interface UserInfo {
  id: number;
  phone: string;
  email: string;
  fullName: string;
  createdDate: string;
  lastLogin?: string;
  loginIdentifier: string;
}

export interface AuthResult {
  success: boolean;
  message: string;
  errorDetails?: string;
}

export interface AuthState {
  isLoggedIn: boolean;
  isLoading: boolean;
  error: string | null;
  user: UserInfo | null;
}

export interface FormState {
  isSubmitting: boolean;
  fieldErrors: { [key: string]: string };
  generalError: string | null;
  success: boolean;
}