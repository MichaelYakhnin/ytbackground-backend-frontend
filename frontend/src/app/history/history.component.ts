import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { YouTubeService } from '../youtube.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './history.component.html',
  styleUrls: ['./history.component.css']
})
export class HistoryComponent implements OnInit {
  historyItems: any[] = [];
  loading: boolean = false;
  currentPage: number = 1;
  itemsPerPage: number = 10;
  totalPages: number = 1;
  allVideoIds: string[] = [];

  constructor(
    private youtubeService: YouTubeService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadHistory();
  }

  get paginatedItems() {
    const start = (this.currentPage - 1) * this.itemsPerPage;
    const end = start + this.itemsPerPage;
    return this.historyItems.slice(start, end);
  }

  loadHistory(): void {
    const videoIds = JSON.parse(localStorage.getItem('videoHistory') || '[]');
    if (videoIds.length > 0) {
      this.allVideoIds = videoIds;
      this.totalPages = Math.ceil(videoIds.length / this.itemsPerPage);
      this.loadCurrentPage();
    }
  }

  loadCurrentPage(): void {
    const start = (this.currentPage - 1) * this.itemsPerPage;
    const end = start + this.itemsPerPage;
    const currentPageIds = this.allVideoIds.slice(start, end);

    if (currentPageIds.length > 0) {
      this.loading = true;
      this.youtubeService.getVideosDetails(currentPageIds).subscribe({
        next: (videos) => {
          this.historyItems = videos;
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading history:', error);
          this.loading = false;
        }
      });
    }
  }

  previousPage(): void {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.loadCurrentPage();
    }
  }

  nextPage(): void {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.loadCurrentPage();
    }
  }

  playVideo(videoId: string): void {
    this.router.navigate(['/video-player'], { queryParams: { videoId } });
  }

  clearHistory(): void {
    localStorage.removeItem('videoHistory');
    this.historyItems = [];
    this.totalPages = 1;
    this.currentPage = 1;
  }
}