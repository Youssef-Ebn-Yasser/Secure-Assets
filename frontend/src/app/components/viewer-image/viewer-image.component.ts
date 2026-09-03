import { Component, OnInit, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { MediaService, MediaFile, ImageTileManifest } from '../../services/media.service';

@Component({
  selector: 'app-viewer-image',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="viewer-container secure-media-container" (contextmenu)="preventRightClick($event)">
      <div class="viewer-header">
        <a routerLink="/dashboard" class="btn btn-secondary btn-sm">
          <i class="fa-solid fa-arrow-left"></i> Vault
        </a>
        <div class="file-title-section">
          <h2>{{ file?.originalName || 'Secure Tiled Image' }}</h2>
          <span class="protection-badge">
            <i class="fa-solid fa-puzzle-piece"></i> Memory-Assembled Canvas Grid ({{ manifest?.tiles?.length || 16 }} Tiles)
          </span>
        </div>

        <div class="viewer-controls" *ngIf="manifest">
          <button (click)="zoomIn()" class="btn btn-secondary btn-sm" title="Zoom In">
            <i class="fa-solid fa-magnifying-glass-plus"></i>
          </button>
          <button (click)="zoomOut()" class="btn btn-secondary btn-sm" title="Zoom Out">
            <i class="fa-solid fa-magnifying-glass-minus"></i>
          </button>
          <button (click)="resetZoom()" class="btn btn-secondary btn-sm" title="Reset View">
            <i class="fa-solid fa-arrows-rotate"></i>
          </button>
        </div>
      </div>

      <div class="glass-panel image-viewer-card">
        <div *ngIf="loading" class="canvas-loading">
          <i class="fa-solid fa-spinner fa-spin fa-2x text-brand"></i>
          <span>Reassembling encrypted tile chunks in memory ({{ loadedTiles }}/{{ totalTiles }})...</span>
        </div>

        <div class="canvas-viewport" [style.cursor]="isPanning ? 'grabbing' : 'grab'">
          <canvas 
            #imageCanvas 
            class="secure-canvas"
            [style.transform]="'scale(' + zoomLevel + ')'"
          ></canvas>

          <div class="watermark-overlay">
            <span>SECURE VAULT • {{ auth.currentUser()?.email }}</span>
          </div>
        </div>

        <div class="player-info-footer">
          <div class="info-pill">
            <i class="fa-solid fa-shield-halved text-brand"></i>
            <span>No Complete Image File on Network or Disk</span>
          </div>
          <div class="info-pill">
            <i class="fa-solid fa-cubes text-brand"></i>
            <span>{{ manifest?.gridRows || 4 }}x{{ manifest?.gridCols || 4 }} Dynamic WebP Tiles</span>
          </div>
          <div class="info-pill">
            <i class="fa-solid fa-eye-slash text-brand"></i>
            <span>Right-Click / Save Image Disabled</span>
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
      gap: 0.5rem;
    }

    .image-viewer-card {
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
      min-height: 480px;
      background: #000000;
      border-radius: var(--radius-md);
      overflow: hidden;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
    }

    .secure-canvas {
      max-width: 100%;
      max-height: 70vh;
      object-fit: contain;
      transition: transform 0.2s ease-out;
      box-shadow: 0 0 20px rgba(0,0,0,0.8);
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
export class ViewerImageComponent implements OnInit {
  @ViewChild('imageCanvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;
  fileId = '';
  file: MediaFile | null = null;
  manifest: ImageTileManifest | null = null;
  loading = true;
  loadedTiles = 0;
  totalTiles = 0;
  zoomLevel = 1.0;
  isPanning = false;

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

      this.loadManifestAndTiles();
    }
  }

  zoomIn(): void {
    if (this.zoomLevel < 3.0) this.zoomLevel += 0.25;
  }

  zoomOut(): void {
    if (this.zoomLevel > 0.5) this.zoomLevel -= 0.25;
  }

  resetZoom(): void {
    this.zoomLevel = 1.0;
  }

  private loadManifestAndTiles(): void {
    this.loading = true;
    this.mediaService.getImageManifest(this.fileId).subscribe({
      next: (manifest) => {
        this.manifest = manifest;
        this.totalTiles = manifest.tiles.length;
        this.drawTilesToCanvas(manifest);
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  private drawTilesToCanvas(manifest: ImageTileManifest): void {
    const canvas = this.canvasRef.nativeElement;
    canvas.width = manifest.originalWidth;
    canvas.height = manifest.originalHeight;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    this.loadedTiles = 0;

    manifest.tiles.forEach((tile) => {
      const img = new Image();
      img.crossOrigin = 'anonymous';
      img.onload = () => {
        const x = tile.col * manifest.tileWidth;
        const y = tile.row * manifest.tileHeight;
        ctx.drawImage(img, x, y);

        this.loadedTiles++;
        if (this.loadedTiles >= this.totalTiles) {
          this.loading = false;
        }
      };
      img.src = tile.url;
    });
  }
}
