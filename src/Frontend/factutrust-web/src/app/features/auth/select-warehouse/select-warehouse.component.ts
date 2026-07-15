import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { LogoComponent } from '@shared/components/logo/logo.component';
import { StockService, Warehouse } from '@core/services/stock.service';
import { WarehouseContextService } from '@core/services/warehouse-context.service';

@Component({
  selector: 'app-select-warehouse',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ButtonModule,
    MessageModule,
    LogoComponent
  ],
  templateUrl: './select-warehouse.component.html',
  styleUrl: './select-warehouse.component.scss'
})
export class SelectWarehouseComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private stockService = inject(StockService);
  private warehouseContext = inject(WarehouseContextService);

  warehouses = signal<Warehouse[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  selectedId: string | null = null;

  ngOnInit(): void {
    this.stockService.getWarehouses(true).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data?.length) {
          const list = res.data;
          this.warehouses.set(list);
          if (list.length === 1) {
            this.selectedId = list[0].id;
            this.confirm();
            return;
          }
          const def = list.find((w) => w.isDefault);
          this.selectedId = def?.id ?? list[0].id;
        } else {
          this.error.set('Aucun entrepôt actif trouvé. Contactez votre administrateur.');
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Impossible de charger les entrepôts. Réessayez plus tard.');
      }
    });
  }

  confirm(): void {
    const id = this.selectedId;
    if (!id) return;
    this.warehouseContext.setSelectedWarehouse(id);
    const returnUrl =
      this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
    const safe = returnUrl.startsWith('/') ? returnUrl : '/dashboard';
    this.router.navigateByUrl(safe);
  }
}
