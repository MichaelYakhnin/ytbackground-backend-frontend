import { Component } from '@angular/core';
import { RouterOutlet, Router } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CommonModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],

})
export class AppComponent {
  title = 'ytbackground-frontend';
  constructor(private router: Router) {}

  ngOnInit(): void {
    // Redirect to login page if token is not present
    if (!localStorage.getItem('token')) {
      this.router.navigate(['/login']);
    }
  }
}