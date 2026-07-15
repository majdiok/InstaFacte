import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NgbModalModule } from '@ng-bootstrap/ng-bootstrap';
import { ToastComponent } from '@shared/components/toast/toast.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NgbModalModule, ToastComponent],
  template: `
    <router-outlet></router-outlet>
    <app-toast-container></app-toast-container>
  `,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
    }
  `]
})
export class AppComponent {
  title = 'InstaFact';
}
