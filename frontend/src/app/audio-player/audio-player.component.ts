import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { YouTubeService } from '../youtube.service';
import { ActivatedRoute } from '@angular/router';
import { HttpResponse } from '@angular/common/http';

@Component({
  selector: 'app-audio-player',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './audio-player.component.html',
  styleUrls: ['./audio-player.component.css']
})
export class AudioPlayerComponent implements OnInit, OnDestroy {
  videoId: string = '';
  title: string = '';
  mode: number = 0; // 0 for play, 1 for save, 2 play from disk
  @ViewChild('audioElement') audioElement!: ElementRef<HTMLAudioElement>;

  //audioUrl: string | undefined;
  audioUrl: SafeUrl | null = null;
  isPlaying: boolean = false;
  currentTime: number = 0;
  duration: number = 0;
  loading: boolean = false;
  volume: number = 1;

  private audioContext: AudioContext | undefined;
  private audioSource: MediaElementAudioSourceNode | undefined;

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

  private cleanup(): void {
    if (this.audioElement?.nativeElement) {
      const audio = this.audioElement.nativeElement;
      audio.pause();
      
      // Clean up any object URLs we created
      if (this.audioUrl) {
        URL.revokeObjectURL(this.audioUrl.toString());
      }
      
      // Reset the audio source
      audio.src = '';
      this.audioUrl = null;
    }

    // Clean up audio context if it exists
    if (this.audioContext) {
      this.audioContext.close();
    }
    
    // Save final playback position
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
      const audio = this.audioElement?.nativeElement;
      const currentTime = audio?.currentTime || 0;
      
      // Calculate range based on current playback position
      // Estimate ~128 KB per second of audio (typical MP3 bitrate)
      const bytesPerSecond = 128 * 1024; // 128 KB/s
      const rangeStart = currentTime > 0 ? Math.floor(currentTime * bytesPerSecond) : 0;
      const range = currentTime > 0 ? `bytes=${rangeStart}-` : undefined;

      this.youtubeService.playFile(this.videoId, range).subscribe({
        next: (blob: Blob) => {
          const url = URL.createObjectURL(blob);
          this.audioUrl = this.sanitizer.bypassSecurityTrustUrl(url);
          this.loading = false;

          if (currentTime > 0 && audio) {
            audio.currentTime = currentTime;
          }
        },
        error: (error) => {
          console.error('Error loading audio:', error);
          if (retryCount < maxRetries) {
            retryCount++;
            console.log(`Retrying... Attempt ${retryCount} of ${maxRetries}`);
            setTimeout(loadWithRetry, 1000 * retryCount); // Exponential backoff
          } else {
            this.loading = false;
          }
        }
      });
    };

    loadWithRetry();
  }

loadFromServer(isSave: boolean = false): void {
      this.youtubeService.getVideoStream(this.videoId, this.title,isSave).subscribe({
        next: (blob) => {
          let url = URL.createObjectURL(blob);
          this.audioUrl = this.sanitizer.bypassSecurityTrustUrl(url);
          this.loading = false;
          //this.initializeAudioContext();
        },
        error: (error) => {
          console.error('Error loading audio:', error);
          this.loading = false;
        }
      });
    }

  private initializeAudioContext(): void {
    // Initialize audio context when user interacts with the player
    if (!this.audioContext) {
      this.audioContext = new AudioContext();
      const audio = this.audioElement.nativeElement;
      this.audioSource = this.audioContext.createMediaElementSource(audio);
      this.audioSource.connect(this.audioContext.destination);
    }
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

  
  togglePlay(): void {
    const audio = this.audioElement.nativeElement;
    if (audio.paused) {
      audio.play();
      this.isPlaying = true;
      //this.saveToHistory(); // Save when starting playback
    } else {
      audio.pause();
      this.isPlaying = false;
      //this.saveToHistory(); // Save when pausing
    }
  }


  onTimeUpdate(): void {
    const audio = this.audioElement.nativeElement;
    this.currentTime = audio.currentTime;
    
    // Save progress every 5 seconds and when there's actual progress
    if (Math.floor(this.currentTime) % 5 === 0 && this.currentTime > 0) {
      this.saveToHistory();
    }

    // Check if we need to preload the next chunk
    const buffered = audio.buffered;
    if (buffered.length > 0) {
      const timeRemaining = buffered.end(buffered.length - 1) - this.currentTime;
      if (timeRemaining < 10 && !this.loading) { // Less than 10 seconds remaining
        console.log('Preloading next chunk...');
        this.loadFromDisk(); // This will now use range request based on current time
      }
    }
  }

  onLoadedMetadata(): void {
    const audio = this.audioElement.nativeElement;
    this.duration = audio.duration;
  }

  seek(time: number): void {
    const audio = this.audioElement.nativeElement;
    audio.currentTime = time;
    //this.saveToHistory(); // Save when seeking to new position
  }

  setVolume(value: number): void {
    const audio = this.audioElement.nativeElement;
    this.volume = value;
    audio.volume = value;
  }

  formatTime(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = Math.floor(seconds % 60);
    return `${minutes}:${remainingSeconds.toString().padStart(2, '0')}`;
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