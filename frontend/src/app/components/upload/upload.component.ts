import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { HttpEventType } from '@angular/common/http';
import { MediaService } from '../../services/media.service';

@Component({
  selector: 'app-upload',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="upload-container">
      <div class="upload-header">
        <a routerLink="/dashboard" class="btn btn-secondary btn-sm">
          <i class="fa-solid fa-arrow-left"></i> Back to Vault
        </a>
        <h1>Upload & Encrypt Media</h1>
        <p class="subtitle">Upload files to transcode into token-gated AES-128 HLS streams, image tiles, or PDF slices.</p>
      </div>

      <div class="glass-panel upload-card">
        <!-- Dropzone Area -->
        <div 
          class="dropzone" 
          [class.drag-over]="isDragging"
          (dragover)="onDragOver($event)"
          (dragleave)="onDragLeave($event)"
          (drop)="onDrop($event)"
          (click)="fileInput.click()"
        >
          <input 
            #fileInput 
            type="file" 
            style="display: none" 
            (change)="onFileSelected($event)" 
            accept="video/*,image/*,.pdf,.mkv,.mov" 
          />

          <div class="drop-icon">
            <i class="fa-solid fa-cloud-arrow-up"></i>
          </div>
          <h3>Choose a file or drag & drop it here</h3>
          <p class="drop-hint">Supported formats: MP4, MOV, MKV, WebM, PNG, JPG, WebP, PDF (Up to 500MB)</p>
          
          <button type="button" class="btn btn-primary" style="margin-top: 1.25rem;">
            <i class="fa-solid fa-folder-open"></i> Browse Files
          </button>
        </div>

        <!-- Selected File Preview -->
        <div *ngIf="selectedFile" class="file-preview-card">
          <div class="file-preview-info">
            <div class="file-preview-icon">
              <i [class]="getFileIcon(selectedFile.name)"></i>
            </div>
            <div class="file-details">
              <span class="preview-name">{{ selectedFile.name }}</span>
              <span class="preview-size">{{ formatBytes(selectedFile.size) }}</span>
            </div>
          </div>

          <div *ngIf="isUploading" class="progress-section">
            <div class="progress-bar-container">
              <div class="progress-bar-fill" [style.width.%]="uploadProgress"></div>
            </div>
            <div class="progress-text">
              <span>Uploading to secure pipeline...</span>
              <span>{{ uploadProgress }}%</span>
            </div>
          </div>

          <div *ngIf="errorMessage" class="error-banner">
            <i class="fa-solid fa-circle-exclamation"></i> {{ errorMessage }}
          </div>

          <div class="preview-actions" *ngIf="!isUploading">
            <button (click)="upload()" class="btn btn-primary">
              <i class="fa-solid fa-shield-halved"></i> Upload & Process into Chunks
            </button>
            <button (click)="cancelSelection()" class="btn btn-secondary">
              Cancel
            </button>
          </div>
        </div>

        <!-- Processing Specs Info -->
        <div class="specs-grid">
          <div class="spec-card">
            <i class="fa-solid fa-film text-brand"></i>
            <div>
              <strong>Video Transcoding</strong>
              <span>HLS 4s chunks with dynamic AES-128 key generation and randomized segment IDs.</span>
            </div>
          </div>
          <div class="spec-card">
            <i class="fa-solid fa-image text-brand"></i>
            <div>
              <strong>Image Slicing</strong>
              <span>4x4 WebP tile grid reassembled purely on HTML5 Canvas in client memory.</span>
            </div>
          </div>
          <div class="spec-card">
            <i class="fa-solid fa-file-pdf text-brand"></i>
            <div>
              <strong>PDF Isolation</strong>
              <span>Rendered high-DPI page slices under randomized server GUID folders.</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .upload-container {
      max-width: 900px;
      margin: 2rem auto;
      padding: 0 1.5rem;
    }

    .upload-header {
      margin-bottom: 1.5rem;
    }

    .upload-header h1 {
      margin-top: 0.75rem;
    }

    .subtitle {
      color: var(--text-secondary);
      font-size: 0.95rem;
    }

    .upload-card {
      padding: 2.5rem;
    }

    .dropzone {
      border: 2px dashed var(--surface-border);
      border-radius: var(--radius-lg);
      padding: 3.5rem 2rem;
      text-align: center;
      background: rgba(0, 0, 0, 0.25);
      cursor: pointer;
      transition: all 0.25s ease;
    }

    .dropzone:hover, .dropzone.drag-over {
      border-color: var(--brand-400);
      background: rgba(22, 163, 74, 0.08);
      transform: scale(1.01);
    }

    .drop-icon {
      font-size: 3.5rem;
      color: var(--brand-400);
      margin-bottom: 1rem;
    }

    .drop-hint {
      color: var(--text-muted);
      font-size: 0.88rem;
      margin-top: 0.5rem;
    }

    .file-preview-card {
      margin-top: 2rem;
      padding: 1.5rem;
      background: var(--surface-dark);
      border: 1px solid var(--surface-border);
      border-radius: var(--radius-md);
    }

    .file-preview-info {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .file-preview-icon {
      width: 48px;
      height: 48px;
      background: rgba(22, 163, 74, 0.2);
      border-radius: var(--radius-md);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.5rem;
      color: var(--brand-300);
    }

    .file-details {
      display: flex;
      flex-direction: column;
    }

    .preview-name {
      font-weight: 600;
      color: #FFFFFF;
    }

    .preview-size {
      font-size: 0.85rem;
      color: var(--text-secondary);
    }

    .progress-section {
      margin-top: 1.25rem;
    }

    .progress-bar-container {
      height: 8px;
      background: rgba(255, 255, 255, 0.1);
      border-radius: var(--radius-full);
      overflow: hidden;
    }

    .progress-bar-fill {
      height: 100%;
      background: linear-gradient(90deg, var(--brand-500), var(--brand-300));
      transition: width 0.2s ease;
    }

    .progress-text {
      display: flex;
      justify-content: space-between;
      font-size: 0.82rem;
      color: var(--brand-200);
      margin-top: 0.4rem;
    }

    .error-banner {
      margin-top: 1rem;
      padding: 0.75rem;
      background: rgba(220, 38, 38, 0.2);
      border: 1px solid rgba(220, 38, 38, 0.4);
      border-radius: var(--radius-sm);
      color: #FCA5A5;
      font-size: 0.88rem;
    }

    .preview-actions {
      display: flex;
      gap: 0.75rem;
      margin-top: 1.5rem;
    }

    .specs-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 1.25rem;
      margin-top: 2.5rem;
      padding-top: 2rem;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
    }

    .spec-card {
      display: flex;
      gap: 0.85rem;
      font-size: 0.85rem;
    }

    .spec-card i {
      font-size: 1.3rem;
      margin-top: 0.15rem;
    }

    .spec-card strong {
      display: block;
      color: #FFFFFF;
      margin-bottom: 0.2rem;
    }

    .spec-card span {
      color: var(--text-secondary);
      line-height: 1.4;
    }
  `]
})
export class UploadComponent {
  isDragging = false;
  selectedFile: File | null = null;
  isUploading = false;
  uploadProgress = 0;
  errorMessage = '';

  constructor(private mediaService: MediaService, private router: Router) {}

  onDragOver(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDragging = true;
  }

  onDragLeave(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDragging = false;
  }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDragging = false;
    if (e.dataTransfer && e.dataTransfer.files.length > 0) {
      this.selectedFile = e.dataTransfer.files[0];
      this.errorMessage = '';
    }
  }

  onFileSelected(e: any): void {
    if (e.target.files && e.target.files.length > 0) {
      this.selectedFile = e.target.files[0];
      this.errorMessage = '';
    }
  }

  cancelSelection(): void {
    this.selectedFile = null;
    this.uploadProgress = 0;
    this.errorMessage = '';
  }

  upload(): void {
    if (!this.selectedFile) return;

    this.isUploading = true;
    this.uploadProgress = 0;
    this.errorMessage = '';

    this.mediaService.uploadFile(this.selectedFile).subscribe({
      next: (event: any) => {
        if (event.type === HttpEventType.UploadProgress) {
          if (event.total) {
            this.uploadProgress = Math.round((100 * event.loaded) / event.total);
          }
        } else if (event.type === HttpEventType.Response) {
          this.isUploading = false;
          this.router.navigate(['/dashboard']);
        }
      },
      error: (err) => {
        this.isUploading = false;
        this.errorMessage = err?.error?.message || 'Upload failed. Please check network and file size.';
      }
    });
  }

  getFileIcon(filename: string): string {
    const ext = filename.split('.').pop()?.toLowerCase() || '';
    if (['mp4', 'mov', 'mkv', 'webm', 'avi'].includes(ext)) return 'fa-solid fa-film';
    if (['png', 'jpg', 'jpeg', 'webp', 'bmp'].includes(ext)) return 'fa-solid fa-image';
    if (['pdf'].includes(ext)) return 'fa-solid fa-file-pdf';
    return 'fa-solid fa-file';
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }
}
