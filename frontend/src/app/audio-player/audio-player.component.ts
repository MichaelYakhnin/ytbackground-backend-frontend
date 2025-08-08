import { Component, ElementRef, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { YouTubeService } from '../youtube.service';
import { ActivatedRoute } from '@angular/router';

// Only import core Videogular functionality
import { VgCoreModule, VgApiService } from '@videogular/ngx-videogular/core';

@Component({
  selector: 'app-audio-player',
  standalone: true,
  imports: [
    CommonModule,
    VgCoreModule
  ],
  templateUrl: './audio-player.component.html',
  styleUrls: ['./audio-player.component.css'],
  providers: [VgApiService]
})
export class AudioPlayerComponent implements OnInit, OnDestroy {
  videoId: string = '';
  title: string = '';
  mode: number = 0; // 0 for play, 1 for save, 2 play from disk
  audioUrl: SafeUrl | null = null;
  isPlaying: boolean = false;
  currentTime: number = 0;
  duration: number = 0;
  loading: boolean = false;
  volume: number = 1;

  private api!: VgApiService;

  constructor(
    private youtubeService: YouTubeService,
    private route: ActivatedRoute,
    private sanitizer: DomSanitizer,
  ) {}

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.videoId = params['videoId'];
      this.title = params['title'];
      this.mode = Number.parseInt(params['mode']);
      this.loadAudio();
    });
  }

  onPlayerReady(api: VgApiService) {
    this.api = api;

    this.api.getDefaultMedia().subscriptions.loadedMetadata.subscribe(
      () => {
        this.duration = this.api.getDefaultMedia().duration;
        // Restore playback position from history if available
        const savedTime = this.loadFromHistory();
        if (savedTime > 0) {
          this.api.seekTime(savedTime);
        }
      }
    );

    this.api.getDefaultMedia().subscriptions.timeUpdate.subscribe(
      () => {
        this.currentTime = this.api.currentTime;
        if (Math.floor(this.currentTime) % 5 === 0 && this.currentTime > 0) {
          this.saveToHistory();
        }
      }
    );

    this.api.getDefaultMedia().subscriptions.playing.subscribe(
      () => {
        this.isPlaying = true;
      }
    );

    this.api.getDefaultMedia().subscriptions.pause.subscribe(
      () => {
        this.isPlaying = false;
      }
    );

    // Set initial volume
    this.api.volume = this.volume;
  }

  private cleanup(): void {
    if (this.api) {
      this.api.pause();
    }
    
    if (this.audioUrl) {
      URL.revokeObjectURL(this.audioUrl.toString());
    }
    
    this.saveToHistory();
  }

  ngOnDestroy(): void {
    this.cleanup();
  }

  loadAudio(): void {
    this.loading = true;
    console.log('Loading audio for videoId:', this.videoId, 'mode:', this.mode);
    switch (this.mode) {
      case 0: // Play
        this.loadFromServer();
        break;
      case 1: // Save
        this.loadFromServer(true);
        break;
      case 2: // Play from disk
        this.loadFromDisk();
        break;
    }
  }

  loadFromDisk() {
    this.loading = true;
    let retryCount = 0;
    const maxRetries = 3;

    const loadWithRetry = () => {
      const currentTime = this.api ? this.api.currentTime : 0;
      
      // Calculate range based on current playback position
      const bytesPerSecond = 128 * 1024; // 128 KB/s
      const rangeStart = currentTime > 0 ? Math.floor(currentTime * bytesPerSecond) : 0;
      const range = currentTime > 0 ? `bytes=${rangeStart}-` : undefined;

      this.youtubeService.playFile(this.videoId, range).subscribe({
        next: (blob: Blob) => {
          const url = URL.createObjectURL(blob);
          this.audioUrl = this.sanitizer.bypassSecurityTrustUrl(url);
          this.loading = false;

          if (currentTime > 0 && this.api) {
            this.api.seekTime(currentTime);
          }
        },
        error: (error) => {
          console.error('Error loading audio:', error);
          if (retryCount < maxRetries) {
            retryCount++;
            console.log(`Retrying... Attempt ${retryCount} of ${maxRetries}`);
            setTimeout(loadWithRetry, 1000 * retryCount);
          } else {
            this.loading = false;
          }
        }
      });
    };

    loadWithRetry();
  }

  loadFromServer(isSave: boolean = false): void {
    this.youtubeService.getVideoStream(this.videoId, this.title, isSave).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        this.audioUrl = this.sanitizer.bypassSecurityTrustUrl(url);
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading audio:', error);
        this.loading = false;
      }
    });
  }

  togglePlay(): void {
    if (this.api.state === 'playing') {
      this.api.pause();
    } else {
      this.api.play();
    }
  }

  seek(value: number): void {
    if (this.api) {
      this.api.seekTime(value);
      this.saveToHistory();
    }
  }

  setVolume(value: number): void {
    this.volume = value;
    if (this.api) {
      this.api.volume = value;
    }
  }

  formatTime(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = Math.floor(seconds % 60);
    return `${minutes}:${remainingSeconds.toString().padStart(2, '0')}`;
  }

  loadFromHistory(): number {
    const audioHistory = JSON.parse(localStorage.getItem('audioHistory') || '[]') as Array<{
      id: string;
      title: string;
      timestamp: string;
      duration: number;
      currentTime: number;
    }>;

    const currentItem = audioHistory.find(item => item.id === this.videoId);
    return currentItem ? currentItem.currentTime : 0;
  }

  private saveToHistory(): void {
    if (!this.videoId || !this.title) {
      console.warn('Cannot save to history: missing videoId or title');
      return;
    }

    const audioHistory = JSON.parse(localStorage.getItem('audioHistory') || '[]') as Array<{
      id: string;
      title: string;
      timestamp: string;
      duration: number;
      currentTime: number;
    }>;

    const existingIndex = audioHistory.findIndex(item => item.id === this.videoId);
    const historyItem = {
      id: this.videoId,
      title: this.title,
      timestamp: new Date().toISOString(),
      duration: this.duration,
      currentTime: this.currentTime
    };

    if (existingIndex !== -1) {
      audioHistory.splice(existingIndex, 1);
    }

    audioHistory.unshift(historyItem);

    if (audioHistory.length > 50) {
      audioHistory.pop();
    }

    try {
      localStorage.setItem('audioHistory', JSON.stringify(audioHistory));
      console.log('Saved to history:', {
        id: this.videoId,
        title: this.title,
        currentTime: this.formatTime(this.currentTime),
        duration: this.formatTime(this.duration)
      });
    } catch (error) {
      console.error('Error saving to history:', error);
    }
  }
}