import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <header class="navbar-header">
      <div class="navbar-container">
        <a routerLink="/" class="navbar-brand">
          <div class="logo-shield">
            <i class="fa-solid fa-shield-halved"></i>
          </div>
          <div class="brand-text">
            <span class="brand-title">Secure Media Vault</span>
            <span class="brand-badge">AES-128 Chunked</span>
          </div>
        </a>

        <nav class="navbar-nav">
          <ng-container *ngIf="auth.currentUser() as user; else anonNav">
            <a routerLink="/dashboard" class="nav-item">
              <i class="fa-solid fa-layer-group"></i> Vault
            </a>
            <a routerLink="/upload" class="nav-item btn-upload-nav">
              <i class="fa-solid fa-cloud-arrow-up"></i> Upload
            </a>
            <div class="user-pill">
              <span class="user-avatar"><i class="fa-solid fa-user-shield"></i></span>
              <span class="user-email">{{ user.email }}</span>
              <button (click)="logout()" class="btn-logout" title="Sign Out">
                <i class="fa-solid fa-right-from-bracket"></i>
              </button>
            </div>
          </ng-container>

          <ng-template #anonNav>
            <a routerLink="/auth" class="btn btn-primary btn-sm">
              <i class="fa-solid fa-key"></i> Sign In / Register
            </a>
          </ng-template>
        </nav>
      </div>
    </header>
  `,
  styles: [`
    .navbar-header {
      background: rgba(10, 15, 13, 0.85);
      backdrop-filter: blur(12px);
      border-bottom: 1px solid var(--surface-border);
      position: sticky;
      top: 0;
      z-index: 1000;
      padding: 0.75rem 1.5rem;
    }

    .navbar-container {
      max-width: 1280px;
      margin: 0 auto;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .navbar-brand {
      display: flex;
      align-items: center;
      gap: 0.85rem;
      text-decoration: none;
    }

    .logo-shield {
      width: 42px;
      height: 42px;
      background: linear-gradient(135deg, var(--brand-700), var(--brand-500));
      border-radius: var(--radius-md);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #FFFFFF;
      font-size: 1.3rem;
      box-shadow: var(--shadow-glow);
    }

    .brand-text {
      display: flex;
      flex-direction: column;
    }

    .brand-title {
      font-size: 1.15rem;
      font-weight: 700;
      color: #FFFFFF;
      letter-spacing: -0.02em;
    }

    .brand-badge {
      font-size: 0.68rem;
      font-family: var(--font-mono);
      color: var(--brand-300);
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }

    .navbar-nav {
      display: flex;
      align-items: center;
      gap: 1.25rem;
    }

    .nav-item {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.95rem;
      font-weight: 500;
      color: var(--text-secondary);
      padding: 0.5rem 0.85rem;
      border-radius: var(--radius-sm);
      transition: all 0.2s ease;
    }

    .nav-item:hover {
      color: var(--brand-300);
      background: var(--surface-hover);
    }

    .btn-upload-nav {
      background: rgba(22, 163, 74, 0.2);
      border: 1px solid rgba(74, 222, 128, 0.3);
      color: var(--brand-300);
    }

    .user-pill {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      background: var(--surface-dark);
      border: 1px solid var(--surface-border);
      padding: 0.35rem 0.75rem;
      border-radius: var(--radius-full);
    }

    .user-avatar {
      color: var(--brand-400);
    }

    .user-email {
      font-size: 0.85rem;
      color: var(--text-primary);
      max-width: 150px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .btn-logout {
      background: transparent;
      border: none;
      color: var(--text-secondary);
      cursor: pointer;
      padding: 0.2rem;
      transition: color 0.2s ease;
    }

    .btn-logout:hover {
      color: var(--danger-500);
    }

    .btn-sm {
      padding: 0.45rem 0.9rem;
      font-size: 0.85rem;
    }
  `]
})
export class NavbarComponent {
  constructor(public auth: AuthService, private router: Router) {}

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/auth']);
  }
}
