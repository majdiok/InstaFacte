import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { BreadcrumbComponent, BreadcrumbItem } from '@shared/components/breadcrumb/breadcrumb.component';
import { SkeletonTableComponent } from '@shared/components/skeleton/skeleton-table.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { ProductService, ProductAttributeDto } from '@core/services/product.service';
import { ToastService } from '@core/services/toast.service';
import { ErrorHandlerService } from '@core/services/error-handler.service';
import { AuthService } from '@core/services/auth.service';
import { PERMISSIONS } from '@core/config/permission-keys';
import { ConfirmationService } from '@core/services/confirmation.service';

@Component({
  selector: 'app-product-attribute-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TableModule,
    InputTextModule,
    ToastModule,
    PageHeaderComponent,
    BreadcrumbComponent,
    SkeletonTableComponent,
    EmptyStateComponent,
    ButtonComponent
  ],
  template: `
    <app-breadcrumb [items]="breadcrumbItems"></app-breadcrumb>

    <app-page-header
      title="Attributs produits"
      subtitle="Gérez les axes de variantes (taille, couleur, etc.) et leurs valeurs.">
      @if (canCreate()) {
        <app-button variant="primary" icon="pi-plus" iconPos="left" routerLink="new">
          Nouvel attribut
        </app-button>
      }
    </app-page-header>

    <div class="ft-filters">
      <span class="p-input-icon-left flex-1">
        <i class="pi pi-search"></i>
        <input
          pInputText
          type="text"
          placeholder="Rechercher par code ou nom..."
          [(ngModel)]="searchTerm"
          class="w-full" />
      </span>
    </div>

    <div class="ft-table-card">
      @if (loading() && attributes().length === 0) {
        <app-skeleton-table [rows]="5" [columns]="[{ width: '20%' }, { width: '30%' }, { width: '30%' }, { width: '20%' }]">
        </app-skeleton-table>
      } @else if (filteredAttributes().length === 0) {
        <app-empty-state
          icon="pi-th-large"
          title="Aucun attribut"
          message="Créez des attributs pour générer des variantes de produits." />
      } @else {
        <p-table [value]="filteredAttributes()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Code</th>
              <th>Nom</th>
              <th>Valeurs</th>
              <th class="actions-col">Actions</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-attr>
            <tr>
              <td><code>{{ attr.code }}</code></td>
              <td>{{ attr.name }}</td>
              <td>{{ attr.values.length }} valeur(s)</td>
              <td class="actions-col">
                @if (canUpdate()) {
                  <app-button variant="ghost" size="sm" icon="pi-pencil" [routerLink]="[attr.id, 'edit']">
                    Modifier
                  </app-button>
                }
                @if (canDelete()) {
                  <app-button variant="ghost" size="sm" icon="pi-trash" (clicked)="confirmDelete(attr)">
                    Supprimer
                  </app-button>
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
  styles: [`
    .ft-filters { margin-bottom: 1rem; }
    .actions-col { width: 200px; text-align: right; }
    .ft-table-card {
      background: var(--surface-card, #fff);
      border-radius: 8px;
      padding: 1rem;
    }
  `]
})
export class ProductAttributeListComponent implements OnInit {
  private productService = inject(ProductService);
  private toastService = inject(ToastService);
  private errorHandler = inject(ErrorHandlerService);
  private auth = inject(AuthService);
  private confirmationService = inject(ConfirmationService);

  loading = signal(false);
  attributes = signal<ProductAttributeDto[]>([]);
  searchTerm = '';

  breadcrumbItems: BreadcrumbItem[] = [
    { label: 'Tableau de bord', route: '/dashboard', icon: 'pi-home' },
    { label: 'Attributs produits' }
  ];

  filteredAttributes = computed(() => {
    const term = this.searchTerm.trim().toLowerCase();
    const items = this.attributes();
    if (!term) return items;
    return items.filter(
      a => a.code.toLowerCase().includes(term) || a.name.toLowerCase().includes(term)
    );
  });

  canCreate = () => this.auth.hasPermission(PERMISSIONS.products.create);
  canUpdate = () => this.auth.hasPermission(PERMISSIONS.products.update);
  canDelete = () => this.auth.hasPermission(PERMISSIONS.products.delete);

  ngOnInit(): void {
    this.loadAttributes();
  }

  private loadAttributes(): void {
    this.loading.set(true);
    this.productService.listAttributes().subscribe({
      next: res => {
        this.loading.set(false);
        if (res.success && res.data) this.attributes.set(res.data);
      },
      error: err => {
        this.loading.set(false);
        this.toastService.add({
          severity: 'error',
          summary: 'Erreur',
          detail: this.errorHandler.extractErrorMessage(err)
        });
      }
    });
  }

  confirmDelete(attr: ProductAttributeDto): void {
    this.confirmationService.confirm({
      header: 'Supprimer l\'attribut',
      message: `Supprimer l'attribut « ${attr.name} » ? Cette action est irréversible.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Supprimer',
      rejectLabel: 'Annuler',
      accept: () => {
        this.productService.deleteAttribute(attr.id).subscribe({
          next: res => {
            if (res.success) {
              this.toastService.add({
                severity: 'success',
                summary: 'Supprimé',
                detail: 'Attribut supprimé'
              });
              this.loadAttributes();
            }
          },
          error: err => {
            this.toastService.add({
              severity: 'error',
              summary: 'Erreur',
              detail: this.errorHandler.extractErrorMessage(err)
            });
          }
        });
      }
    });
  }
}
