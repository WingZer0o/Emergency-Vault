const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 1000 * 60 * 60 * 24 * 365],
  ['month', 1000 * 60 * 60 * 24 * 30],
  ['week', 1000 * 60 * 60 * 24 * 7],
  ['day', 1000 * 60 * 60 * 24],
  ['hour', 1000 * 60 * 60],
  ['minute', 1000 * 60],
];

const formatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });

/** "3 weeks ago" style relative time for a record's updatedAt/createdAt timestamp. */
export function relativeTime(isoDate: string): string {
  const diffMs = new Date(isoDate).getTime() - Date.now();
  for (const [unit, unitMs] of UNITS) {
    if (Math.abs(diffMs) >= unitMs) {
      return formatter.format(Math.round(diffMs / unitMs), unit);
    }
  }
  return formatter.format(0, 'minute');
}
