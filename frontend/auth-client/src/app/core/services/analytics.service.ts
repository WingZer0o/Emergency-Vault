import { Injectable } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

import { environment } from '../../../environments/environment';

declare global {
  interface Window {
    dataLayer: unknown[];
    gtag: (...args: unknown[]) => void;
  }
}

/**
 * Wires Google Analytics (GA4) into the SPA. gtag.js only auto-tracks the page it's loaded on,
 * so this subscribes to the router's NavigationEnd events and sends a page_view for every
 * in-app navigation (the initial config call has send_page_view disabled to avoid double
 * counting that first one). A no-op when environment.gaMeasurementId is empty, so local dev
 * never sends traffic to GA.
 */
@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  constructor(private readonly router: Router) {
    if (!environment.gaMeasurementId) {
      return;
    }

    this.loadGtag(environment.gaMeasurementId);

    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        window.gtag('event', 'page_view', {
          page_path: event.urlAfterRedirects,
        });
      });
  }

  private loadGtag(measurementId: string): void {
    const script = document.createElement('script');
    script.async = true;
    script.src = `https://www.googletagmanager.com/gtag/js?id=${measurementId}`;
    document.head.appendChild(script);

    window.dataLayer = window.dataLayer ?? [];
    window.gtag = function gtag(...args: unknown[]) {
      window.dataLayer.push(args);
    };
    window.gtag('js', new Date());
    window.gtag('config', measurementId, { send_page_view: false });
  }
}
