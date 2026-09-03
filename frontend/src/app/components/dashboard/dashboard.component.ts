import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MediaService, MediaFile } from '../../services/media.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="dashboard-container">
      <div class="dashboard-header">
        <div>
          <h1>Secured Media Repository</h1>
          <p class="subtitle">Token-gated chunk manifests with memory-only browser reassembly</p>
        </div>
        <div class="header-actions">
          <button (click)="loadFiles()" class="btn btn-secondary" [disabled]="loading">
            <i class="fa-solid fa-arrows-rotate" [class.fa-spin]="loading"></i> Refresh
          </button>
          <a routerLink="/upload" class="btn btn-primary">
            <i class="fa-solid fa-upload"></i> Upload Media
          </a>
        </div>
      </div>

      <!-- Security Status Banner -->
      <div class="glass-panel security-banner">
        <div class="sec-item">
          <i class="fa-solid fa-lock text-brand"></i>
          <div>
            <span class="sec-title">Storage Isolation</span>
            <span class="sec-desc">MinIO private network without public ports</span>
          </div>
        </div>
        <div class="sec-item">
          <i class="fa-solid fa-puzzle-piece text-brand"></i>
          <div>
            <span class="sec-title">Chunk Architecture</span>
            <span class="sec-desc">HLS AES-128, Image Tiling, PDF Page Slices</span>
          </div>
        </div>
        <div class="sec-item">
          <i class="fa-solid fa-stopwatch text-brand"></i>
          <div>
            <span class="sec-title">Short-Lived HMAC</span>
            <span class="sec-desc">Tokens expire in ~30s with replay prevention</span>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div *ngIf="!loading && files.length === 0" class="glass-panel empty-state">
        <div class="empty-icon">
          <i class="fa-solid fa-folder-open"></i>
        </div>
        <h3>No media files in vault</h3>
        <p>Upload a video, image, or PDF to begin processing into secure non-downloadable chunks.</p>
        <a routerLink="/upload" class="btn btn-primary" style="margin-top: 1rem;">
          <i class="fa-solid fa-cloud-arrow-up"></i> Upload First File
        </a>
      </div>

      <!-- Files Grid -->
      <div *ngIf="files.length > 0" class="files-grid">
        <div *ngFor="let file of files" class="glass-panel file-card">
          <div class="file-card-header">
            <div class="file-type-badge" [ngClass]="getTypeClass(file.mediaType)">
              <i [class]="getTypeIcon(file.mediaType)"></i>
            </div>
            <div class="file-info">
              <h4 class="file-name" [title]="file.originalName">{{ file.originalName }}</h4>
              <span class="file-meta">
                {{ formatBytes(file.fileSizeBytes) }} • {{ file.createdAt | date:'short' }}
              </span>
            </div>
          </div>

          <div class="file-card-body">
            <div class="status-row">
              <span class="status-label">Status:</span>
              <span [class]="getStatusBadgeClass(file.status)">
                <i [class]="getStatusIcon(file.status)"></i>
                {{ getStatusText(file.status) }}
              </span>
            </div>

            <div class="status-row" *ngIf="file.chunkCount">
              <span class="status-label">Protected Chunks:</span>
              <span class="chunk-count-tag">
                <i class="fa-solid fa-cubes"></i> {{ file.chunkCount }} parts
              </span>
            </div>
          </div>

          <div class="file-card-footer">
            <a 
              *ngIf="file.status === 2" 
              [routerLink]="getViewUrl(file)" 
              class="btn btn-primary btn-sm flex-1"
            >
              <i class="fa-solid fa-eye"></i> Secure View
            </a>
            <span *ngIf="file.status === 1" class="processing-tag flex-1">
              <i class="fa-solid fa-spinner fa-spin"></i> Processing...
            </span>
            <span *ngIf="file.status === 3" class="failed-tag flex-1">
              <i class="fa-solid fa-triangle-exclamation"></i> Error
            </span>

            <button (click)="deleteFile(file.id)" class="btn btn-secondary btn-icon" title="Delete">
              <i class="fa-solid fa-trash"></i>
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-container {
      max-width: 1280px;
      margin: 2rem auto;
      padding: 0 1.5rem;
    }

    .dashboard-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 1rem;
      margin-bottom: 1.5rem;
    }

    .subtitle {
      color: var(--text-secondary);
      font-size: 0.95rem;
      margin-top: 0.25rem;
    }

    .header-actions {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .security-banner {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 1.5rem;
      padding: 1.25rem 1.75rem;
      margin-bottom: 2rem;
      border-left: 4px solid var(--brand-500);
    }

    .sec-item {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .sec-item i {
      font-size: 1.5rem;
    }

    .text-brand {
      color: var(--brand-400);
    }

    .sec-title {
      display: block;
      font-size: 0.92rem;
      font-weight: 700;
      color: #FFFFFF;
    }

    .sec-desc {
      font-size: 0.8rem;
      color: var(--text-secondary);
    }

    .empty-state {
      text-align: center;
      padding: 4rem 2rem;
      margin-top: 2rem;
    }

    .empty-icon {
      font-size: 3rem;
      color: var(--brand-400);
      margin-bottom: 1rem;
      opacity: 0.8;
    }

    .files-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 1.5rem;
    }

    .file-card {
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      transition: transform 0.2s ease, border-color 0.2s ease;
    }

    .file-card:hover {
      transform: translateY(-2px);
      border-color: rgba(74, 222, 128, 0.4);
    }

    .file-card-header {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-bottom: 1rem;
    }

    .file-type-badge {
      width: 44px;
      height: 44px;
      border-radius: var(--radius-md);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.25rem;
      flex-shrink: 0;
    }

    .badge-video {
      background: rgba(168, 85, 247, 0.2);
      color: #C084FC;
      border: 1px solid rgba(168, 85, 247, 0.3);
    }

    .badge-image {
      background: rgba(22, 163, 74, 0.2);
      color: var(--brand-300);
      border: 1px solid rgba(74, 222, 128, 0.3);
    }

    .badge-pdf {
      background: rgba(239, 68, 68, 0.2);
      color: #F87171;
      border: 1px solid rgba(239, 68, 68, 0.3);
    }

    .file-info {
      overflow: hidden;
    }

    .file-name {
      font-size: 0.95rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .file-meta {
      font-size: 0.78rem;
      color: var(--text-muted);
    }

    .file-card-body {
      padding: 0.75rem 0;
      border-top: 1px solid rgba(255, 255, 255, 0.05);
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      margin-bottom: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .status-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 0.85rem;
    }

    .status-label {
      color: var(--text-secondary);
    }

    .chunk-count-tag {
      font-family: var(--font-mono);
      font-size: 0.8rem;
      color: var(--brand-300);
      background: rgba(22, 163, 74, 0.15);
      padding: 0.15rem 0.5rem;
      border-radius: var(--radius-sm);
    }

    .file-card-footer {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .flex-1 {
      flex: 1;
    }

    .btn-icon {
      padding: 0.55rem 0.75rem;
    }

    .processing-tag {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.4rem;
      font-size: 0.85rem;
      font-weight: 600;
      color: #FCD34D;
      padding: 0.55rem;
      background: rgba(245, 158, 11, 0.1);
      border-radius: var(--radius-md);
    }

    .failed-tag {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.4rem;
      font-size: 0.85rem;
      font-weight: 600;
      color: #FCA5A5;
      padding: 0.55rem;
      background: rgba(220, 38, 38, 0.1);
      border-radius: var(--radius-md);
    }
  `]
})
export class DashboardComponent implements OnInit, OnDestroy {
  files: MediaFile[] = [];
  loading = false;
  private pollTimer: any;

  constructor(private mediaService: MediaService) {}

  ngOnInit(): void {
    this.loadFiles();
    // Poll every 4 seconds for processing updates
    this.pollTimer = setInterval(() => {
      if (this.files.some(f => f.status === 0 || f.status === 1)) {
        this.loadFiles(false);
      }
    }, 4000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) clearInterval(this.pollTimer);
  }

  loadFiles(showLoading = true): void {
    if (showLoading) this.loading = true;
    this.mediaService.getFiles().subscribe({
      next: (files) => {
        this.files = files;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  deleteFile(id: string): void {
    if (confirm('Are you sure you want to delete this media file and all encrypted chunks?')) {
      this.mediaService.deleteFile(id).subscribe(() => {
        this.files = this.files.filter(f => f.id !== id);
      });
    }
  }

  getViewUrl(file: MediaFile): string {
    switch (file.mediaType) {
      case 1: return `/viewer/video/${file.id}`;
      case 2: return `/viewer/image/${file.id}`;
      case 3: return `/viewer/pdf/${file.id}`;
      default: return '/dashboard';
    }
  }

  getTypeClass(type: number): string {
    switch (type) {
      case 1: return 'badge-video';
      case 2: return 'badge-image';
      case 3: return 'badge-pdf';
      default: return '';
    }
  }

  getTypeIcon(type: number): string {
    switch (type) {
      case 1: return 'fa-solid fa-film';
      case 2: return 'fa-solid fa-image';
      case 3: return 'fa-solid fa-file-pdf';
      default: return 'fa-solid fa-file';
    }
  }

  getStatusBadgeClass(status: number): string {
    switch (status) {
      case 0: return 'badge badge-warning';
      case 1: return 'badge badge-warning';
      case 2: return 'badge badge-success';
      case 3: return 'badge badge-danger';
      default: return 'badge badge-info';
    }
  }

  getStatusIcon(status: number): string {
    switch (status) {
      case 0: return 'fa-solid fa-clock';
      case 1: return 'fa-solid fa-spinner fa-spin';
      case 2: return 'fa-solid fa-shield-check';
      case 3: return 'fa-solid fa-circle-xmark';
      default: return 'fa-solid fa-circle-info';
    }
  }

  getStatusText(status: number): string {
    switch (status) {
      case 0: return 'Queued';
      case 1: return 'Chunking';
      case 2: return 'Protected';
      case 3: return 'Failed';
      default: return 'Unknown';
    }
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }
}
