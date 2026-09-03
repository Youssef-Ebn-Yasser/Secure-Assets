import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface User {
  id: string;
  email: string;
  role: string;
}

export interface AuthResponse {
  token: string;
  userId: string;
  email: string;
  role: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private tokenKey = 'vault_token';
  private userKey = 'vault_user';

  currentUser = signal<User | null>(this.getStoredUser());
  token = signal<string | null>(localStorage.getItem(this.tokenKey));

  constructor(private http: HttpClient) {}

  register(credentials: { email: string; password: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/register', credentials).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  login(credentials: { email: string; password: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', credentials).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    this.token.set(null);
    this.currentUser.set(null);
  }

  isAuthenticated(): boolean {
    return !!this.token();
  }

  getToken(): string | null {
    return this.token();
  }

  private handleAuthSuccess(res: AuthResponse): void {
    const user: User = {
      id: res.userId,
      email: res.email,
      role: res.role
    };
    localStorage.setItem(this.tokenKey, res.token);
    localStorage.setItem(this.userKey, JSON.stringify(user));
    this.token.set(res.token);
    this.currentUser.set(user);
  }

  private getStoredUser(): User | null {
    const data = localStorage.getItem(this.userKey);
    if (!data) return null;
    try {
      return JSON.parse(data);
    } catch {
      return null;
    }
  }
}
