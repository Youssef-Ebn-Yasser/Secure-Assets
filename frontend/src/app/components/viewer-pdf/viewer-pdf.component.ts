import { Component, OnInit, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { MediaService, MediaFile, PdfManifest } from '../../services/media.service';

@Component({
  selector: 'app-viewer-pdf',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="viewer-container secure-media-container" (contextmenu)="preventRightClick($event)">
      <div class="viewer-header">
        <a routerLink="/dashboard" class="btn btn-secondary btn-sm">
          <i class="fa-solid fa-arrow-left"></i> Vault
        </a>
        <div class="file-title-section">
          <h2>{{ file?.originalName || 'Secure Document' }}</h2>
          <span class="protection-badge">
            <i class="fa-solid fa-file-shield"></i> Page Slice Isolation ({{ manifest?.totalPages || 0 }} Pages)
          </span>
        </div>

        <!-- Navigation Controls -->
        <div class="viewer-controls" *ngIf="manifest">
          <button (click)="prevPage()" class="btn btn-secondary btn-sm" [disabled]="currentPage <= 1">
            <i class="fa-solid fa-chevron-left"></i>
          </button>
          <div class="page-indicator">
            <span>Page</span>
            <input 
              type="number" 
              [(ngModel)]="currentPage" 
              (change)="goToPage(currentPage)" 
              [min]="1" 
              [max]="manifest.totalPages" 
              class="page-input"
            />
            <span>/ {{ manifest.totalPages }}</span>
          </div>
          <button (click)="nextPage()" class="btn btn-secondary btn-sm" [disabled]="currentPage >= manifest.totalPages">
            <i class="fa-solid fa-chevron-right"></i>
          </button>

          <div class="zoom-controls">
            <button (click)="zoomIn()" class="btn btn-secondary btn-sm" title="Zoom In">
              <i class="fa-solid fa-magnifying-glass-plus"></i>
            </button>
            <button (click)="zoomOut()" class="btn btn-secondary btn-sm" title="Zoom Out">
              <i class="fa-solid fa-magnifying-glass-minus"></i>
            </button>
          </div>
        </div>
      </div>

      <div class="glass-panel pdf-viewer-card">
        <div *ngIf="loading" class="canvas-loading">
          <i class="fa-solid fa-spinner fa-spin fa-2x text-brand"></i>
          <span>Decrypting and rendering page {{ currentPage }}...</span>
        </div>

        <div class="canvas-viewport">
          <canvas 
            #pdfCanvas 
            class="secure-canvas"
            [style.transform]="'scale(' + zoomLevel + ')'"
          ></canvas>

          <div class="watermark-overlay">
            <span>SECURE VAULT • {{ auth.currentUser()?.email }}</span>
          </div>
        </div>

        <div class="player-info-footer">
          <div class="info-pill">
            <i class="fa-solid fa-lock text-brand"></i>
            <span>No Complete .pdf Download Available</span>
          </div>
          <div class="info-pill">
            <i class="fa-solid fa-folder-tree text-brand"></i>
            <span>Randomized Server GUID Paths</span>
          </div>
          <div class="info-pill">
            <i class="fa-solid fa-clock text-brand"></i>
            <span>Page Token Expiry: 60s</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .viewer-container {
      max-width: 1100px;
      margin: 2rem auto;
      padding: 0 1.5rem;
    }

    .viewer-header {
      display: flex;
      align-items: center;
      gap: 1.25rem;
      margin-bottom: 1.5rem;
      flex-wrap: wrap;
    }

    .file-title-section {
      flex: 1;
    }

    .file-title-section h2 {
      font-size: 1.35rem;
    }

    .protection-badge {
      font-size: 0.75rem;
      font-family: var(--font-mono);
      color: var(--brand-300);
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      background: rgba(22, 163, 74, 0.15);
      border: 1px solid rgba(74, 222, 128, 0.3);
      padding: 0.2rem 0.5rem;
      border-radius: var(--radius-sm);
      margin-top: 0.25rem;
    }

    .viewer-controls {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .page-indicator {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.88rem;
      color: var(--text-secondary);
      background: rgba(255, 255, 255, 0.05);
      padding: 0.2rem 0.5rem;
      border-radius: var(--radius-sm);
    }

    .page-input {
      width: 45px;
      background: rgba(0, 0, 0, 0.5);
      border: 1px solid var(--surface-border);
      border-radius: var(--radius-sm);
      color: #FFFFFF;
      text-align: center;
      padding: 0.2rem;
      font-weight: 600;
    }

    .zoom-controls {
      display: flex;
      gap: 0.35rem;
      margin-left: 0.5rem;
    }

    .pdf-viewer-card {
      padding: 1.5rem;
      background: var(--surface-dark);
    }

    .canvas-loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
      padding: 3rem 1rem;
      color: var(--text-secondary);
      font-size: 0.9rem;
    }

    .canvas-viewport {
      position: relative;
      width: 100%;
      min-height: 580px;
      background: #000000;
      border-radius: var(--radius-md);
      overflow: hidden;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1.5rem;
    }

    .secure-canvas {
      max-width: 100%;
      max-height: 75vh;
      object-fit: contain;
      background: #FFFFFF;
      box-shadow: 0 0 25px rgba(0, 0, 0, 0.9);
      transition: transform 0.2s ease-out;
    }

    .watermark-overlay {
      position: absolute;
      bottom: 20px;
      right: 20px;
      pointer-events: none;
      background: rgba(0, 0, 0, 0.6);
      padding: 0.3rem 0.75rem;
      border-radius: var(--radius-sm);
      font-size: 0.75rem;
      font-family: var(--font-mono);
      color: rgba(74, 222, 128, 0.7);
      border: 1px solid rgba(74, 222, 128, 0.2);
    }

    .player-info-footer {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      margin-top: 1.5rem;
      padding-top: 1rem;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
    }

    .info-pill {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.85rem;
      color: var(--text-secondary);
      background: rgba(255, 255, 255, 0.03);
      padding: 0.4rem 0.85rem;
      border-radius: var(--radius-full);
    }

    .text-brand {
      color: var(--brand-400);
    }
  `]
})
export class ViewerPdfComponent implements OnInit {
  @ViewChild('pdfCanvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;
  fileId = '';
  file: MediaFile | null = null;
  manifest: PdfManifest | null = null;
  currentPage = 1;
  loading = true;
  zoomLevel = 1.0;

  constructor(
    private route: ActivatedRoute,
    private mediaService: MediaService,
    public auth: AuthService
  ) {}

  @HostListener('window:keydown', ['$event'])
  handleKeyDown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
      event.preventDefault();
    }
    if (event.key === 'ArrowRight') this.nextPage();
    if (event.key === 'ArrowLeft') this.prevPage();
  }

  preventRightClick(event: MouseEvent): void {
    event.preventDefault();
  }

  ngOnInit(): void {
    this.fileId = this.route.snapshot.paramMap.get('id') || '';
    if (this.fileId) {
      this.mediaService.getFile(this.fileId).subscribe({
        next: (f) => this.file = f
      });

      this.loadPdfManifest();
    }
  }

  prevPage(): void {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.renderCurrentPage();
    }
  }

  nextPage(): void {
    if (this.manifest && this.currentPage < this.manifest.totalPages) {
      this.currentPage++;
      this.renderCurrentPage();
    }
  }

  goToPage(page: number): void {
    if (this.manifest && page >= 1 && page <= this.manifest.totalPages) {
      this.currentPage = page;
      this.renderCurrentPage();
    }
  }

  zoomIn(): void {
    if (this.zoomLevel < 2.5) this.zoomLevel += 0.2;
  }

  zoomOut(): void {
    if (this.zoomLevel > 0.6) this.zoomLevel -= 0.2;
  }

  private loadPdfManifest(): void {
    this.loading = true;
    this.mediaService.getPdfManifest(this.fileId).subscribe({
      next: (manifest) => {
        this.manifest = manifest;
        this.renderCurrentPage();
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  private renderCurrentPage(): void {
    if (!this.manifest) return;
    const pageItem = this.manifest.pages.find(p => p.pageNumber === this.currentPage);
    if (!pageItem) return;

    this.loading = true;
    const canvas = this.canvasRef.nativeElement;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      canvas.width = img.width;
      canvas.height = img.height;
      ctx.drawImage(img, 0, 0);
      this.loading = false;

      // Prefetch next page
      if (this.manifest && this.currentPage < this.manifest.totalPages) {
        const next = this.manifest.pages.find(p => p.pageNumber === this.currentPage + 1);
        if (next) {
          const preImg = new Image();
          preImg.src = next.url;
        }
      }
    };
    img.src = pageItem.url;
  }
}
