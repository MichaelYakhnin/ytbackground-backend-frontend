import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { YouTubeService } from '../youtube.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-saved-files',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './saved-files.component.html',
  styleUrls: ['./saved-files.component.css']
})
export class SavedFilesComponent implements OnInit {
  loading: boolean = false;
  playingVideoId: string | null = null;
  currentPage: number = 1;
  itemsPerPage: number = 10;
  totalPages: number = 1;
  files: string[] = [];
  currentPageFiles: string[] = [];

  constructor(
    private youtubeService: YouTubeService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadSavedFiles();
  }

  loadSavedFiles(): void {
    this.loading = true;
    this.files = [];
    this.currentPageFiles = [];
    
    this.youtubeService.getSavedFiles().subscribe({
      next: (files) => {
        if (files && files.length > 0) {
          this.files = files;
          this.totalPages = Math.ceil(this.files.length / this.itemsPerPage);
          this.currentPage = 1; // Reset to first page when loading new files
          this.loadCurrentPage();
        } else {
          this.totalPages = 1;
          this.currentPage = 1;
          this.files = [];
          this.currentPageFiles = [];
        }
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading saved files:', error);
        this.loading = false;
        this.files = [];
        this.currentPageFiles = [];
        this.totalPages = 1;
        this.currentPage = 1;
      }
    });
  }

  loadCurrentPage(): void {
    const start = (this.currentPage - 1) * this.itemsPerPage;
    const end = start + this.itemsPerPage;
    this.currentPageFiles = this.files.slice(start, end);
    this.scrollToTop();
  }

  playVideo(videoId: string): void {
    this.router.navigate(['/audio-player', videoId, '', '2']);
  }

  previousPage(): void {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.loadCurrentPage();
    }
  }

  get canGoToPreviousPage(): boolean {
    return this.currentPage > 1;
  }

  get canGoToNextPage(): boolean {
    return this.currentPage < this.totalPages;
  }

  nextPage(): void {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.loadCurrentPage();
    }
  }
  scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}