import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { VaultItemShareDto, VaultShareStatus, VaultSharedWithMeDto } from '../../../core/models/vault.models';
import { VaultService } from '../../../core/services/vault.service';
import { extractErrorMessage } from '../../auth/auth-error.util';

@Component({
  selector: 'app-shared-with-me-list',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatListModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './shared-with-me-list.html',
  styleUrl: './shared-with-me-list.scss',
})
export class SharedWithMeList implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly vaultService = inject(VaultService);
  private readonly router = inject(Router);

  /** Fixed page size - the paginator itself is only shown once a list exceeds this. */
  readonly pageSize = 10;

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly shares = signal<VaultSharedWithMeDto[]>([]);
  /** Items shared with the current user individually - separate from shares(), which is whole-vault only. */
  readonly itemShares = signal<VaultItemShareDto[]>([]);

  readonly sharesPageIndex = signal(0);
  readonly itemSharesPageIndex = signal(0);

  readonly pagedShares = computed(() => {
    const start = this.sharesPageIndex() * this.pageSize;
    return this.shares().slice(start, start + this.pageSize);
  });

  readonly pagedItemShares = computed(() => {
    const start = this.itemSharesPageIndex() * this.pageSize;
    return this.itemShares().slice(start, start + this.pageSize);
  });

  /** Which share's inline "enter code" form is currently open, if any - shareId is unique across
   * both shares() and itemShares(), so one signal covers whichever list it came from. */
  readonly verifyingId = signal<string | null>(null);
  readonly verifying = signal(false);
  readonly verifyError = signal<string | null>(null);
  readonly resendMessage = signal<string | null>(null);

  readonly codeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  ngOnInit(): void {
    this.loadShares();
    this.loadItemShares();
  }

  statusLabel(status: VaultShareStatus): string {
    switch (status) {
      case 'Active':
        return 'Ready to open';
      case 'Pending':
        return 'Enter the code we emailed you';
      case 'CodeExpired':
        return 'Code expired - resend to try again';
      case 'Failed':
        return 'Too many attempts - ask them to share again';
    }
  }

  onItemClick(share: VaultSharedWithMeDto): void {
    if (share.status === 'Active') {
      this.router.navigateByUrl(`/vault/shared-with-me/${share.ownerUserId}`);
      return;
    }
    if (share.status === 'Failed') {
      return;
    }
    this.verifyingId.set(this.verifyingId() === share.id ? null : share.id);
    this.verifyError.set(null);
    this.resendMessage.set(null);
    this.codeForm.reset();
  }

  onItemShareClick(share: VaultItemShareDto): void {
    if (share.status === 'Active') {
      this.router.navigateByUrl(`/vault/shared-with-me/${share.ownerUserId}/items/${share.itemId}`);
      return;
    }
    if (share.status === 'Failed') {
      return;
    }
    this.verifyingId.set(this.verifyingId() === share.shareId ? null : share.shareId);
    this.verifyError.set(null);
    this.resendMessage.set(null);
    this.codeForm.reset();
  }

  submitCode(shareId: string): void {
    if (this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }

    this.verifying.set(true);
    this.verifyError.set(null);
    const code = this.codeForm.getRawValue().code;

    this.vaultService.verifyShare(shareId, { code }).subscribe({
      next: () => {
        this.verifying.set(false);
        this.verifyingId.set(null);
        this.loadShares();
        this.loadItemShares();
      },
      error: (err) => {
        this.verifying.set(false);
        this.verifyError.set(extractErrorMessage(err, 'Incorrect code.'));
      },
    });
  }

  onSharesPage(event: PageEvent): void {
    this.sharesPageIndex.set(event.pageIndex);
  }

  onItemSharesPage(event: PageEvent): void {
    this.itemSharesPageIndex.set(event.pageIndex);
  }

  resendCode(shareId: string): void {
    this.verifyError.set(null);
    this.resendMessage.set(null);
    this.vaultService.resendShareCode(shareId).subscribe({
      next: () => this.resendMessage.set('A new code has been sent.'),
      error: (err) => this.verifyError.set(extractErrorMessage(err, 'Could not resend code.')),
    });
  }

  private loadShares(): void {
    this.loading.set(true);
    this.vaultService.getSharedWithMe().subscribe({
      next: (shares) => {
        this.shares.set(shares);
        this.sharesPageIndex.set(0);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Could not load vaults shared with you.'));
      },
    });
  }

  private loadItemShares(): void {
    this.vaultService.getItemSharesHeldByMe().subscribe({
      next: (shares) => {
        this.itemShares.set(shares);
        this.itemSharesPageIndex.set(0);
      },
      // Non-critical - whole-vault shares above still work if this list fails to load.
      error: () => {},
    });
  }
}
