import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="auth-wrapper">
      <div class="glass-panel auth-card">
        <div class="auth-header">
          <div class="auth-icon">
            <i class="fa-solid fa-lock"></i>
          </div>
          <h2>{{ isRegister ? 'Create Vault Account' : 'Sign In to Vault' }}</h2>
          <p class="auth-subtitle">
            {{ isRegister ? 'Set up an encrypted storage space' : 'Enter your credentials to access secured media' }}
          </p>
        </div>

        <div *ngIf="errorMessage" class="auth-alert error">
          <i class="fa-solid fa-circle-exclamation"></i>
          <span>{{ errorMessage }}</span>
        </div>

        <form (ngSubmit)="submit()" class="auth-form">
          <div class="form-group">
            <label class="form-label">Email Address</label>
            <input 
              type="email" 
              class="form-control" 
              [(ngModel)]="email" 
              name="email" 
              placeholder="user@example.com"
              required 
            />
          </div>

          <div class="form-group">
            <label class="form-label">Password</label>
            <input 
              type="password" 
              class="form-control" 
              [(ngModel)]="password" 
              name="password" 
              placeholder="••••••••••••"
              required 
            />
          </div>

          <button type="submit" class="btn btn-primary btn-block" [disabled]="loading">
            <i *ngIf="loading" class="fa-solid fa-spinner fa-spin"></i>
            <span>{{ isRegister ? 'Register' : 'Sign In' }}</span>
          </button>
        </form>

        <div class="auth-toggle">
          <span>{{ isRegister ? 'Already have an account?' : "Don't have an account?" }}</span>
          <button (click)="toggleMode()" class="btn-link">
            {{ isRegister ? 'Sign In' : 'Register' }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .auth-wrapper {
      min-height: calc(100vh - 150px);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2rem 1rem;
    }

    .auth-card {
      width: 100%;
      max-width: 440px;
      padding: 2.5rem;
    }

    .auth-header {
      text-align: center;
      margin-bottom: 2rem;
    }

    .auth-icon {
      width: 54px;
      height: 54px;
      background: rgba(22, 163, 74, 0.2);
      border: 1px solid rgba(74, 222, 128, 0.4);
      color: var(--brand-300);
      border-radius: var(--radius-full);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.5rem;
      margin: 0 auto 1rem;
    }

    .auth-subtitle {
      font-size: 0.88rem;
      color: var(--text-secondary);
      margin-top: 0.35rem;
    }

    .auth-alert {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.75rem 1rem;
      border-radius: var(--radius-sm);
      margin-bottom: 1.25rem;
      font-size: 0.85rem;
    }

    .auth-alert.error {
      background: rgba(220, 38, 38, 0.15);
      border: 1px solid rgba(220, 38, 38, 0.3);
      color: #FCA5A5;
    }

    .btn-block {
      width: 100%;
      margin-top: 0.5rem;
      padding: 0.85rem;
    }

    .auth-toggle {
      text-align: center;
      margin-top: 1.75rem;
      font-size: 0.88rem;
      color: var(--text-secondary);
    }

    .btn-link {
      background: none;
      border: none;
      color: var(--brand-400);
      font-weight: 600;
      margin-left: 0.35rem;
      cursor: pointer;
    }

    .btn-link:hover {
      text-decoration: underline;
    }
  `]
})
export class AuthComponent {
  isRegister = false;
  email = '';
  password = '';
  loading = false;
  errorMessage = '';

  constructor(private auth: AuthService, private router: Router) {}

  toggleMode(): void {
    this.isRegister = !this.isRegister;
    this.errorMessage = '';
  }

  submit(): void {
    if (!this.email || !this.password) {
      this.errorMessage = 'Please enter both email and password.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    const req = this.isRegister
      ? this.auth.register({ email: this.email, password: this.password })
      : this.auth.login({ email: this.email, password: this.password });

    req.subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err?.error?.message || 'Authentication failed. Please try again.';
      }
    });
  }
}
