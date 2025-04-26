import { Routes } from '@angular/router';
import { VideoPlayerComponent } from './video-player/video-player.component';
import { LoginComponent } from './login/login.component';
import { HistoryComponent } from './history/history.component';
import { SavedFilesComponent } from './app-saved-files/saved-files.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
    { path: 'video-player', component: VideoPlayerComponent },
    { path: 'history', component: HistoryComponent },
    { path: 'saved-files', component: SavedFilesComponent },
    { path: '', redirectTo: '/login', pathMatch: 'full' },
];