import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { YouTubeService } from '../youtube.service';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { Router } from '@angular/router';

@Component({
  selector: 'app-saved-files',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './saved-files.component.html',
  styleUrls: ['./saved-files.component.css']
})
export class SavedFilesComponent implements OnInit {
  savedFiles: any[] = [];
  loading: boolean = false;
  audioUrl: SafeUrl | null = null;
  playingVideoId: string | null = null;
  currentPage: number = 1;
  itemsPerPage: number = 10;
  totalPages: number = 1;
  allVideoIds: string[] = [];

  constructor(
    private youtubeService: YouTubeService,
    private sanitizer: DomSanitizer,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadSavedFiles();
  }

  loadSavedFiles(): void {
    this.loading = true;
    this.youtubeService.getSavedFiles().subscribe({
      next: (files) => {
        if (files && files.length > 0) {
          this.allVideoIds = files.map(file => file.replace('.mp4', ''));
          this.totalPages = Math.ceil(this.allVideoIds.length / this.itemsPerPage);
          this.loadCurrentPage();
        } else {
          this.savedFiles = [];
          this.loading = false;
          this.totalPages = 1;
        }
      },
      error: (error) => {
        console.error('Error loading saved files:', error);
        this.loading = false;
      }
    });
  }

  loadCurrentPage(): void {
    const start = (this.currentPage - 1) * this.itemsPerPage;
    const end = start + this.itemsPerPage;
    const currentPageIds = this.allVideoIds.slice(start, end);

    if (currentPageIds.length > 0) {
      this.youtubeService.getVideosDetails(currentPageIds).subscribe({
        next: (videos) => {
          this.savedFiles = videos;
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading video details:', error);
          this.loading = false;
        }
      });
    } else {
      this.savedFiles = [];
      this.loading = false;
    }
  }

  playVideo(videoId: string): void {
    if (this.playingVideoId === videoId) {
      this.playingVideoId = null;
      this.audioUrl = null;
      return;
    }
    this.youtubeService.playFile(videoId + '.mp4').subscribe(blob => {
      let url = URL.createObjectURL(blob);
      this.audioUrl = this.sanitizer.bypassSecurityTrustUrl(url);
      this.playingVideoId = videoId;
      this.scrollToTop();
    });
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
  scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}