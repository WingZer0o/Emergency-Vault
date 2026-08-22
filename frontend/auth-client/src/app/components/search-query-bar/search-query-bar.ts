import { Component, model } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-search-query-bar',
  imports: [FormsModule],
  templateUrl: './search-query-bar.html',
  styleUrls: ['./search-query-bar.scss'],
})
export class SearchQueryBar {
  readonly searchQuery = model<string | null>(null);
}
