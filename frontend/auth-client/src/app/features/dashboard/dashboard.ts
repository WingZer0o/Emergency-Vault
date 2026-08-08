import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { UserProfile } from '../../core/models/auth.models';

@Component({
  selector: 'app-dashboard',
  imports: [MatCardModule, MatListModule, MatChipsModule, MatProgressSpinnerModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly authService = inject(AuthService);

  readonly users = signal<UserProfile[] | null>(null);
  readonly usersLoading = signal(false);

  ngOnInit(): void {
    if (this.authService.hasRole('Admin')) {
      this.loadUsers();
    }
  }

  private loadUsers(): void {
    this.usersLoading.set(true);
    this.http.get<UserProfile[]>(`${environment.apiUrl}/users`).subscribe({
      next: (users) => {
        this.users.set(users);
        this.usersLoading.set(false);
      },
      error: () => this.usersLoading.set(false),
    });
  }
}
