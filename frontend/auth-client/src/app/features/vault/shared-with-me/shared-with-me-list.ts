import {
  Component,
  OnInit,
  Signal,
  computed,
  inject,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import {
  VaultItemShareDto,
  VaultShareStatus,
  VaultSharedWithMeDto,
} from '../../../core/models/vault.models';
import { PaginationService } from '../../../core/services/pagination.service';
import { VaultService } from '../../../core/services/vault.service';
import { Paginator } from '../../../shared/paginator/paginator';
import { SearchQueryBar } from '../../../components/search-query-bar/search-query-bar';
import { extractErrorMessage } from '../../auth/auth-error.util';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs';

@Component({
  selector: 'app-shared-with-me-list',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatListModule,
    MatProgressSpinnerModule,
    Paginator,
    SearchQueryBar,
  ],
  templateUrl: './shared-with-me-list.html',
  styleUrl: './shared-with-me-list.scss',
})
export class SharedWithMeList implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly vaultService = inject(VaultService);
  private readonly router = inject(Router);
  private readonly pagination = inject(PaginationService);

  private readonly sharesSearchQueryBar =
    viewChild.required<SearchQueryBar>('sharesSearchQueryBar');
  private readonly itemSharesSearchQueryBar = viewChild.required<SearchQueryBar>(
    'itemSharesSearchQueryBar',
  );

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly shares = signal<VaultSharedWithMeDto[]>([]);
  /** Items shared with the current user individually - separate from shares(), which is whole-vault only. */
  readonly itemShares = signal<VaultItemShareDto[]>([]);

  readonly filteredShares: Signal<VaultSharedWithMeDto[]> = computed(() => {
    const query = this.sharesSearchQueryBar().debouncedSearchQuery()?.toLowerCase().trim() ?? '';
    if (!query) return this.shares();
    return this.shares().filter((share) => share.ownerName.toLowerCase().includes(query));
  });
  readonly filteredItemShares: Signal<VaultItemShareDto[]> = computed(() => {
    const query =
      this.itemSharesSearchQueryBar().debouncedSearchQuery()?.toLowerCase().trim() ?? '';
    if (!query) return this.itemShares();
    return this.itemShares().filter((share) => share.ownerName.toLowerCase().includes(query));
  });

  // Editing a query re-filters the list under its paginator, so send the user back to page 1
  // rather than leaving them stranded past the end of a now-shorter list.
  readonly sharesPageIndex = linkedSignal({
    source: () => this.sharesSearchQueryBar().debouncedSearchQuery(),
    computation: () => 0,
  });
  readonly itemSharesPageIndex = linkedSignal({
    source: () => this.itemSharesSearchQueryBar().debouncedSearchQuery(),
    computation: () => 0,
  });

  readonly pagedShares = this.pagination.slice(this.filteredShares, this.sharesPageIndex);
  readonly pagedItemShares = this.pagination.slice(
    this.filteredItemShares,
    this.itemSharesPageIndex,
  );

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

  statusTagClass(status: VaultShareStatus): string {
    switch (status) {
      case 'Active':
        return 'tag-accent';
      case 'Failed':
        return 'tag-outline';
      default:
        return 'tag-neutral';
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
