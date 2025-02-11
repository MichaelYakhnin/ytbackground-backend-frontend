import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class YouTubeService {
  private apiUrl = '/api/youtube';
  private authUrl = '/api/auth';

  constructor(private http: HttpClient) {}

  getVideoStream(videoId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/stream?videoId=${videoId}`, { responseType: 'blob' });
  }
  
  searchVideos(query: string, maxResults: number): Observable<any> {
    return this.http.get(`${this.apiUrl}/search?query=${query}&maxResults=${maxResults}`);
  }

  login(username: string, password: string): Observable<any> {
    return this.http.post(`${this.authUrl}/login`, { username, password });
  }

  setToken(token: string): void {
    localStorage.setItem('token', token);
  }

  getToken(): string | null {
    return localStorage.getItem('token');
  }

  removeToken(): void {
    localStorage.removeItem('token');
  }
}