import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';

import { ConfirmDialog, ConfirmDialogData } from './confirm-dialog';

/**
 * App-wide confirmation prompt. Inject this rather than opening MatDialog directly so every
 * "are you sure?" moment in the app (deleting a profile, revoking access, etc.) looks and behaves
 * the same way.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly dialog = inject(MatDialog);

  /** Resolves true if the user confirmed, false if they cancelled or dismissed the dialog. */
  confirm(data: ConfirmDialogData): Observable<boolean> {
    const dialogRef = this.dialog.open(ConfirmDialog, {
      data,
      width: '420px',
      autoFocus: 'dialog',
    });

    return dialogRef.afterClosed().pipe(map((confirmed) => confirmed === true));
  }
}
