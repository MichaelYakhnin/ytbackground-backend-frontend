import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class YouTubeService {
  baseUrl = '';//'http://localhost:5054'; // Adjust this to your backend URL
  private apiUrl = this.baseUrl + '/api/youtube';
  private authUrl = this.baseUrl + '/api/auth';

  constructor(private http: HttpClient) {}

  getVideoStream(videoId: string, title: string, saveToFile: boolean = false): Observable<Blob> {
    let params = new HttpParams().set('videoId', videoId).set('title', title);
    if (saveToFile) {
      params = params.set('saveToFile', 'true');
    }
    return this.http.get(`${this.apiUrl}/stream`, { params: params, responseType: 'blob' });
  }

  searchVideos(query: string, maxResults: number): Observable<any> {
    return this.http.get(`${this.apiUrl}/search?query=${query}&maxResults=${maxResults}`);
  }

  getVideosDetails(videoIds: string[]): Observable<any> {
    const params = new HttpParams()
      .set('ids', videoIds.join(','));
    return this.http.get(`${this.apiUrl}/videos`, { params });
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

  getSavedFiles(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/savedFiles`);
  }

  playFile(fileName: string, range?: string): Observable<Blob> {
    const params = new HttpParams().set('fileName', fileName);
    const headers = range ? { 'Range': range } : undefined;
    return this.http.get(`${this.apiUrl}/playFile`, {
      params,
      responseType: 'blob',
      headers
    });
  }
}