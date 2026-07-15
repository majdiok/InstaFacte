import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FirmContextService } from '@core/services/firm-context.service';

@Component({
  selector: 'app-firm-open-dossier',
  standalone: true,
  template: `<p class="p-4">Ouverture du dossier…</p>`
})
export class FirmOpenDossierComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly firmContext = inject(FirmContextService);

  ngOnInit(): void {
    const tenantId = this.route.snapshot.paramMap.get('tenantId');
    if (!tenantId) {
      void this.router.navigate(['/firm/clients']);
      return;
    }
    void this.firmContext.switchClient(tenantId).then(() => {
      void this.router.navigate(['/accounting/chart']);
    }).catch(() => {
      void this.router.navigate(['/firm/clients']);
    });
  }
}
