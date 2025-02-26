import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YouTubeService } from '../youtube.service';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-video-player',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './video-player.component.html',
  styleUrls: ['./video-player.component.css']
})
export class VideoPlayerComponent implements OnInit{
  videoUrl: string | undefined;
  videoId: string = '';
  videoTitle: string = '';
  query: string = '';
  maxResults: number = 10; // Add maxResults property
  searchResults: any[] = [];
  page: number = 1;
  pageSize: number = 25;
  totalResults: any[] = [];
  loading: boolean = false;
  maxResultsOptions: number[] = [5, 10, 25, 50, 100]; // Add maxResultsOptions property

  constructor(private youtubeService: YouTubeService,
    private route: ActivatedRoute) {}
    
  private saveToHistory(videoId: string): void {
    const history = JSON.parse(localStorage.getItem('videoHistory') || '[]');
    if (!history.includes(videoId)) {
      history.unshift(videoId); // Add to beginning of array
      localStorage.setItem('videoHistory', JSON.stringify(history));
    }
  }

  ngOnInit(): void {
    // Get video ID from URL query parameters
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
      this.saveToHistory(this.videoId);
      this.youtubeService.getVideoStream(this.videoId).subscribe({
        next: blob => {
        this.videoUrl = URL.createObjectURL(blob);
        this.loading = false;
      },
      error: (e) => {
        console.error(e);
        this.loading = false;
    },
    complete() {
      console.log("is completed");      
    }
    });
    }
  }
  
  searchVideos(): void {
    if (this.query) {
      this.loading = true;
      this.youtubeService.searchVideos(this.query, this.maxResults).subscribe({
        next:results => {
        this.searchResults = results.slice((this.page - 1) * this.pageSize, this.page * this.pageSize);
        this.totalResults = results;
        this.loading = false;
      }, error: (e) => {
        this.loading = false;
      }
    });
    }
  }

  playVideo(videoId: string, videoTitle: string): void {
    this.videoId = videoId;
    this.videoTitle = videoTitle;
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