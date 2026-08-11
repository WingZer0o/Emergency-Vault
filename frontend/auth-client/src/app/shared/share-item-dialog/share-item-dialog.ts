import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';

export interface ShareItemDialogData {
  itemLabel: string;
}

export interface ShareItemDialogResult {
  email: string;
  canEdit: boolean;
}

@Component({
  selector: 'app-share-item-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatRadioModule],
  templateUrl: './share-item-dialog.html',
  styleUrl: './share-item-dialog.scss',
})
export class ShareItemDialog {
  protected readonly data = inject<ShareItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ShareItemDialog, ShareItemDialogResult>);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    canEdit: [false],
  });

  onCancel(): void {
    this.dialogRef.close();
  }

  onShare(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.dialogRef.close({ email: raw.email, canEdit: raw.canEdit });
  }
}
