import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter, map } from 'rxjs';

import { AuthService } from './core/services/auth.service';
import { ThemeMode, ThemeService } from './core/services/theme.service';
import { VaultSessionService } from './core/services/vault-session.service';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);
  private readonly vaultSession = inject(VaultSessionService);
  private readonly router = inject(Router);

  // The landing page ('/') is its own self-contained marketing page with a Modernist-styled nav
  // of its own, so the app-wide Material toolbar is hidden there. Tracked as a signal (rather
  // than reading router.url directly in the template) so it updates on navigation.
  protected readonly isLandingPage = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects === '/'),
    ),
    { initialValue: this.router.url === '/' },
  );

  protected readonly themeIcons: Record<ThemeMode, string> = {
    light: 'light_mode',
    dark: 'dark_mode',
    system: 'brightness_auto',
  };

  setThemeMode(mode: ThemeMode): void {
    this.themeService.setMode(mode);
  }

  logout(): void {
    this.authService.logout().subscribe(() => {
      this.vaultSession.lock().then(() => {
        this.router.navigateByUrl('/login');
      });
    });
  }
}
