import { Component, model } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { debounceTime } from 'rxjs';

@Component({
  selector: 'app-search-query-bar',
  imports: [FormsModule],
  templateUrl: './search-query-bar.html',
  styleUrls: ['./search-query-bar.scss'],
})
export class SearchQueryBar {
  readonly searchQuery = model<string | null>(null);
  public readonly debouncedSearchQuery = toSignal(
    toObservable(this.searchQuery).pipe(debounceTime(200)),
    { initialValue: null },
  );
}
