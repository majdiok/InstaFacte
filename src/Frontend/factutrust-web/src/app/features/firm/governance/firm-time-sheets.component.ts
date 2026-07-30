import { Component } from '@angular/core';
import { TimeSheetsPageComponent } from './time-sheets/time-sheets-page.component';

@Component({
  selector: 'app-firm-time-sheets',
  standalone: true,
  imports: [TimeSheetsPageComponent],
  template: `<app-time-sheets-page />`
})
export class FirmTimeSheetsComponent {}
