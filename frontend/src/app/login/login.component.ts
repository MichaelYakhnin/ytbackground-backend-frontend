import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YouTubeService } from '../youtube.service';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, CommonModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  username = '';
  password = '';
  errorMessage = '';

  constructor(private youtubeService: YouTubeService, private router: Router) {}

  login(): void {
    this.youtubeService.login(this.username, this.password).subscribe({
      next: (response) => {
        this.youtubeService.setToken(response.token);
        this.router.navigate(['/video-player']); // Redirect to video player page
      },
      error: (error) => {
        this.errorMessage = 'Invalid username or password.';
        console.error('Login failed', error);
      }
    });
  }
}