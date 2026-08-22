import { Component, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-search-query-bar',
  imports: [MatCardModule, MatFormFieldModule, MatInputModule, FormsModule],
  templateUrl: './search-query-bar.html',
  styleUrls: ['./search-query-bar.scss'],
})
export class SearchQueryBar {
  readonly searchQuery = model<string | null>(null);
}
