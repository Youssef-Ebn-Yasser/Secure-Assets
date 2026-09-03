import { Routes } from '@angular/router';
import { AuthComponent } from './components/auth/auth.component';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { UploadComponent } from './components/upload/upload.component';
import { ViewerVideoComponent } from './components/viewer-video/viewer-video.component';
import { ViewerImageComponent } from './components/viewer-image/viewer-image.component';
import { ViewerPdfComponent } from './components/viewer-pdf/viewer-pdf.component';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'auth', component: AuthComponent },
  { path: 'dashboard', component: DashboardComponent },
  { path: 'upload', component: UploadComponent },
  { path: 'viewer/video/:id', component: ViewerVideoComponent },
  { path: 'viewer/image/:id', component: ViewerImageComponent },
  { path: 'viewer/pdf/:id', component: ViewerPdfComponent },
  { path: '**', redirectTo: 'dashboard' }
];
