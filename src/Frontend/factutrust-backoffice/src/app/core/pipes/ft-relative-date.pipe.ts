import { Pipe, PipeTransform } from '@angular/core';

/**
 * Affiche une date au format relatif court en français de Tunisie.
 *
 * Exemples :
 *  - `il y a 2 min`
 *  - `il y a 5 h`
 *  - `il y a 3 j`
 *  - `il y a 2 mois`
 *  - `il y a 1 an`
 *  - `dans 3 j` (futur)
 *
 * Pour la valeur absolue (tooltip), utiliser le pipe `date` standard d'Angular
 * avec format `dd/MM/yyyy HH:mm`.
 */
@Pipe({
  name: 'ftRelativeDate',
  standalone: true,
  pure: true
})
export class FtRelativeDatePipe implements PipeTransform {
  private static formatter = new Intl.RelativeTimeFormat('fr-TN', {
    numeric: 'auto',
    style: 'short'
  });

  transform(value: string | Date | null | undefined): string {
    if (value === null || value === undefined || value === '') {
      return '—';
    }

    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }

    const diffMs = date.getTime() - Date.now();
    const absMs = Math.abs(diffMs);

    const minute = 60 * 1000;
    const hour = 60 * minute;
    const day = 24 * hour;
    const week = 7 * day;
    const month = 30 * day;
    const year = 365 * day;

    let value_: number;
    let unit: Intl.RelativeTimeFormatUnit;

    if (absMs < minute) {
      return 'à l’instant';
    } else if (absMs < hour) {
      value_ = Math.round(diffMs / minute);
      unit = 'minute';
    } else if (absMs < day) {
      value_ = Math.round(diffMs / hour);
      unit = 'hour';
    } else if (absMs < week) {
      value_ = Math.round(diffMs / day);
      unit = 'day';
    } else if (absMs < month) {
      value_ = Math.round(diffMs / week);
      unit = 'week';
    } else if (absMs < year) {
      value_ = Math.round(diffMs / month);
      unit = 'month';
    } else {
      value_ = Math.round(diffMs / year);
      unit = 'year';
    }

    return FtRelativeDatePipe.formatter.format(value_, unit);
  }
}
