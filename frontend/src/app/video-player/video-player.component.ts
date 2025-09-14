import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YouTubeService } from '../youtube.service';
import { ActivatedRoute, Router } from '@angular/router';

@Component({
  selector: 'app-video-player',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './video-player.component.html',
  styleUrls: ['./video-player.component.css']
})
export class VideoPlayerComponent implements OnInit {
  videoUrl: string | undefined;
  showAudioPlayer: boolean = false;
  videoId: string = '';
  videoTitle: string = '';
  query: string = '';
  maxResults: number = 10;
  searchResults: any[] = [];
  page: number = 1;
  pageSize: number = 25;
  totalResults: any[] = [];
  loading: boolean = false;
  maxResultsOptions: number[] = [5, 10, 25, 50, 100];

  constructor(
    private youtubeService: YouTubeService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      if (params['videoId']) {
        this.videoId = params['videoId'];
        this.loadVideo();
      }
    });
  }

  loadVideo(): void {
    if (this.videoId) {
      this.loading = true;
      this.youtubeService.getVideosDetails([this.videoId]).subscribe({
        next: videos => {
          this.searchResults = videos;
          this.totalResults = videos;
          this.loading = false;
        },
        error: (e) => {
          console.error('Error fetching video details', e);
          this.loading = false;
        }
      });
    }
  }

  searchVideos(): void {
    if (this.query) {
      this.loading = true;
      this.youtubeService.searchVideos(this.query, this.maxResults).subscribe({
        next: results => {
          this.searchResults = results.slice((this.page - 1) * this.pageSize, this.page * this.pageSize);
          this.totalResults = results;
          this.loading = false;
        },
        error: (e) => {
          console.error('Error fetching video details', e);
          this.loading = false;
        }
      });
    }
  }

  playVideo(videoId: string, videoTitle: string): void {
    this.saveToHistory(videoId);
    this.router.navigate(['/audio-player', videoId, videoTitle, '0']);
  }

  playAndSaveVideo(videoId: string, videoTitle: string): void {
    this.saveToHistory(videoId);
    this.router.navigate(['/audio-player', videoId, videoTitle, '1']);
  }

  private saveToHistory(videoId: string): void {
    const history = JSON.parse(localStorage.getItem('videoHistory') || '[]') as string[];
    const existingIndex = history.indexOf(videoId);

    if (existingIndex !== -1) {
      history.splice(existingIndex, 1);
    }

    history.unshift(videoId);

    if (history.length > 50) {
      history.pop();
    }

    localStorage.setItem('videoHistory', JSON.stringify(history));
  }

  previousPage(): void {
    if (this.page > 1) {
      this.page--;
      this.searchVideos();
    }
  }

  nextPage(): void {
    if (this.page * this.pageSize < this.totalResults.length) {
      this.page++;
      this.searchVideos();
    }
  }
  scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}