import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YouTubeService } from '../youtube.service';

@Component({
  selector: 'app-video-player',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './video-player.component.html',
  styleUrls: ['./video-player.component.css']
})
export class VideoPlayerComponent {
  videoUrl: string | undefined;
  videoId: string = '';
  query: string = '';
  searchResults: any[] = [];
  page: number = 1;
  pageSize: number = 25;
  totalResults: any[] = [];
  loading: boolean = false;

  constructor(private youtubeService: YouTubeService) {}

  loadVideo(): void {
    if (this.videoId) {
      this.loading = true;
      this.youtubeService.getVideoStream(this.videoId).subscribe(blob => {
        this.videoUrl = URL.createObjectURL(blob);
        this.loading = false;
      }, () => {
        this.loading = false;
      });
    }
  }
  
  searchVideos(): void {
    if (this.query) {
      this.loading = true;
      this.youtubeService.searchVideos(this.query).subscribe(results => {
        this.searchResults = results.slice((this.page - 1) * this.pageSize, this.page * this.pageSize);
        this.totalResults = results;
        this.loading = false;
      }, () => {
        this.loading = false;
      });
    }
  }

  playVideo(videoId: string): void {
    this.videoId = videoId;
    this.loadVideo();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  nextPage(): void {
    if (this.page * this.pageSize < this.totalResults.length) {
      this.page++;
      this.searchResults = this.totalResults.slice((this.page - 1) * this.pageSize, this.page * this.pageSize);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  previousPage(): void {
    if (this.page > 1) {
      this.page--;
      this.searchResults = this.totalResults.slice((this.page - 1) * this.pageSize, this.page * this.pageSize);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }
}