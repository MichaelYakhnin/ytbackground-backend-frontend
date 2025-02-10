
import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpInterceptor
} from '@angular/common/http';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {

  constructor() {}

  intercept(request: HttpRequest<any>, next: HttpHandler) {
    const token = localStorage.getItem('token'); // Get the token from local storage

    if (token) {
      // Clone the request and add the Authorization header
      const authReq = request.clone({
        headers: request.headers.set('Authorization', `Bearer ${token}`)
      });

      // Pass the cloned request to the next handler
      return next.handle(authReq);
    } else {
      // If there is no token, continue with the original request
      return next.handle(request);
    }
  }
}