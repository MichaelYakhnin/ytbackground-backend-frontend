import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class YouTubeService {
  private apiUrl = '/api/youtube';

  constructor(private http: HttpClient) {}

  getVideoStream(videoId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/stream?videoId=${videoId}`, { responseType: 'blob' });
  }
  
  searchVideos(query: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/search?query=${query}`);
  }
}