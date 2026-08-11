import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { VaultItemViewDto } from '../../core/models/vault.models';
import { VaultService } from '../../core/services/vault.service';
import { extractErrorMessage } from '../../features/auth/auth-error.util';

export interface ItemViewersDialogData {
  itemId: string;
}

@Component({
  selector: 'app-item-viewers-dialog',
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatListModule, MatProgressSpinnerModule],
  templateUrl: './item-viewers-dialog.html',
  styleUrl: './item-viewers-dialog.scss',
})
export class ItemViewersDialog implements OnInit {
  private readonly data = inject<ItemViewersDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ItemViewersDialog>);
  private readonly vaultService = inject(VaultService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly viewers = signal<VaultItemViewDto[]>([]);

  ngOnInit(): void {
    this.vaultService.getItemViews(this.data.itemId).subscribe({
      next: (views) => {
        this.viewers.set(views);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(extractErrorMessage(err, 'Could not load viewers.'));
        this.loading.set(false);
      },
    });
  }

  close(): void {
    this.dialogRef.close();
  }
}
