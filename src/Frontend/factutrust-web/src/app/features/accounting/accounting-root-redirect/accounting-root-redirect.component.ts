import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-accounting-root-redirect',
  standalone: true,
  template: ''
})
export class AccountingRootRedirectComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    const target = this.auth.isAccountingFirm() ? '/accounting/home' : '/accounting/financial-statements';
    void this.router.navigateByUrl(target, { replaceUrl: true });
  }
}
