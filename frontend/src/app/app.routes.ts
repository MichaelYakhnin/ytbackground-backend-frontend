import { Routes } from '@angular/router';
import { VideoPlayerComponent } from './video-player/video-player.component';
import { LoginComponent } from './login/login.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
    { path: 'video-player', component: VideoPlayerComponent },
    { path: '', redirectTo: '/login', pathMatch: 'full' },
];