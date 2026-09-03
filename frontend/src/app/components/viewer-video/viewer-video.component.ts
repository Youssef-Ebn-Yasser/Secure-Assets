import { Component, OnInit, OnDestroy, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import Hls from 'hls.js';
import { AuthService } from '../../services/auth.service';
import { MediaService, MediaFile } from '../../services/media.service';

@Component({
  selector: 'app-viewer-video',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="viewer-container secure-media-container" (contextmenu)="preventRightClick($event)">
      <div class="viewer-header">
        <a routerLink="/dashboard" class="btn btn-secondary btn-sm">
          <i class="fa-solid fa-arrow-left"></i> Vault
        </a>
        <div class="file-title-section">
          <h2>{{ file?.originalName || 'Secure Video Stream' }}</h2>
          <span class="protection-badge">
            <i class="fa-solid fa-shield-halved"></i> AES-128 HLS Encrypted Chunks
          </span>
        </div>
      </div>

      <div class="glass-panel player-card">
        <div class="video-wrapper">
          <video 
            #videoPlayer 
            class="secure-video" 
            controls 
            controlsList="nodownload noplaybackrate"
            disablePictureInPicture
            playsinline
          ></video>
          
          <!-- Security Watermark Overlay -->
          <div class="watermark-overlay">
            <span>SECURE VAULT • {{ auth.currentUser()?.email }}</span>
          </div>
        </div>

        <div class="player-info-footer">
          <div class="info-pill">
            <i class="fa-solid fa-microchip text-brand"></i>
            <span>Memory MediaSource Playback</span>
          </div>
          <div class="info-pill">
            <i class="fa-solid fa-key text-brand"></i>
            <span>Signed Tokenized Key Exchange</span>
          </div>
          <div class="info-pill">
            <i class="fa-solid fa-ban text-brand"></i>
            <span>Direct Downloads Blocked</span>
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

    .player-card {
      padding: 1.5rem;
      background: var(--surface-dark);
    }

    .video-wrapper {
      position: relative;
      width: 100%;
      background: #000000;
      border-radius: var(--radius-md);
      overflow: hidden;
      aspect-ratio: 16 / 9;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .secure-video {
      width: 100%;
      height: 100%;
      object-fit: contain;
    }

    .watermark-overlay {
      position: absolute;
      bottom: 25px;
      right: 25px;
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
export class ViewerVideoComponent implements OnInit, OnDestroy {
  @ViewChild('videoPlayer', { static: true }) videoElement!: ElementRef<HTMLVideoElement>;
  fileId = '';
  file: MediaFile | null = null;
  private hls: Hls | null = null;

  constructor(
    private route: ActivatedRoute,
    private mediaService: MediaService,
    public auth: AuthService
  ) {}

  @HostListener('window:keydown', ['$event'])
  handleKeyDown(event: KeyboardEvent): void {
    // Block Ctrl+S / Cmd+S
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
        next: (f) => {
          this.file = f;
          this.initHlsPlayer();
        }
      });
    }
  }

  ngOnDestroy(): void {
    if (this.hls) {
      this.hls.destroy();
    }
  }

  private initHlsPlayer(): void {
    const video = this.videoElement.nativeElement;
    const manifestUrl = `/api/stream/${this.fileId}/manifest.m3u8`;
    const token = this.auth.getToken();

    if (Hls.isSupported()) {
      this.hls = new Hls({
        xhrSetup: (xhr, url) => {
          if (token) {
            xhr.setRequestHeader('Authorization', `Bearer ${token}`);
          }
        }
      });

      this.hls.loadSource(manifestUrl);
      this.hls.attachMedia(video);
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
      video.src = manifestUrl;
    }
  }
}
