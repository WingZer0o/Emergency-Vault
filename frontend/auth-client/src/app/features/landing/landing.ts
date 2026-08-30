import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';
import { DownloadLinks } from '../../shared/download-links/download-links';

@Component({
  selector: 'app-landing',
  imports: [RouterLink, DownloadLinks],
  templateUrl: './landing.html',
  styleUrl: './landing.scss',
})
export class Landing {
  public readonly authService = inject(AuthService);

  public scrollToSection(sectionId: string): void {
    const element = document.getElementById(sectionId);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth' });
      console.log('welcome');
    }
  }
}
