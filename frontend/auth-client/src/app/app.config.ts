import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { refreshInterceptor } from './core/interceptors/refresh.interceptor';
import { AnalyticsService } from './core/services/analytics.service';
import { AuthService } from './core/services/auth.service';
import { VaultSessionService } from './core/services/vault-session.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([authInterceptor, refreshInterceptor])),
    provideAppInitializer(() => firstValueFrom(inject(AuthService).tryRestoreSession())),
    // Restores an unlocked vault session from IndexedDB (if any) before the router runs, so
    // vaultUnlockedGuard sees the correct isUnlocked() state on the very first navigation
    // instead of momentarily redirecting to the unlock screen after every refresh.
    provideAppInitializer(() => inject(VaultSessionService).restoreFromStorage()),
    // AnalyticsService is providedIn: 'root' but otherwise never injected anywhere, so this
    // forces Angular to construct it (and start listening to router events) at bootstrap.
    provideAppInitializer(() => void inject(AnalyticsService)),
  ],
};
