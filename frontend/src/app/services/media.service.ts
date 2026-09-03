import { Injectable } from '@angular/core';
import { HttpClient, HttpEvent, HttpRequest } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface MediaFile {
  id: string;
  originalName: string;
  mediaType: number; // 1: Video, 2: Image, 3: Pdf
  status: number;    // 0: Pending, 1: Processing, 2: Ready, 3: Failed
  fileSizeBytes: number;
  createdAt: string;
  processedAt?: string;
  chunkCount?: number;
}

export interface ImageTileManifest {
  fileId: string;
  gridRows: number;
  gridCols: number;
  originalWidth: number;
  originalHeight: number;
  tileWidth: number;
  tileHeight: number;
  tiles: {
    row: number;
    col: number;
    tileId: string;
    url: string;
  }[];
}

export interface PdfManifest {
  fileId: string;
  totalPages: number;
  pages: {
    pageNumber: number;
    url: string;
  }[];
}

@Injectable({
  providedIn: 'root'
})
export class MediaService {
  constructor(private http: HttpClient) {}

  getFiles(): Observable<MediaFile[]> {
    return this.http.get<MediaFile[]>('/api/media');
  }

  getFile(id: string): Observable<MediaFile> {
    return this.http.get<MediaFile>(`/api/media/${id}`);
  }

  getFileStatus(id: string): Observable<any> {
    return this.http.get<any>(`/api/media/${id}/status`);
  }

  deleteFile(id: string): Observable<void> {
    return this.http.delete<void>(`/api/media/${id}`);
  }

  uploadFile(file: File): Observable<HttpEvent<any>> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    const req = new HttpRequest('POST', '/api/media/upload', formData, {
      reportProgress: true
    });

    return this.http.request(req);
  }

  getImageManifest(fileId: string): Observable<ImageTileManifest> {
    return this.http.get<ImageTileManifest>(`/api/image/${fileId}/manifest`);
  }

  getPdfManifest(fileId: string): Observable<PdfManifest> {
    return this.http.get<PdfManifest>(`/api/pdf/${fileId}/manifest`);
  }
}
