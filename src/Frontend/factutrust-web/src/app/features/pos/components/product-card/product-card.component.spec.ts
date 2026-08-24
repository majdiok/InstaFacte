import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { ProductCardComponent } from './product-card.component';
import { ProductListItem, ProductService } from '@core/services/product.service';
import { PosFavoritesService } from '../../services/pos-favorites.service';
import { PosStateService } from '../../services/pos-state.service';

function product(overrides: Partial<ProductListItem> = {}): ProductListItem {
  return {
    id: 'p-1',
    code: 'CH-01',
    name: 'Chemise',
    description: null,
    typeDisplay: 'Produit',
    categoryId: 'cat-1',
    category: 'Cat',
    unitPrice: 10,
    vatRate: 19,
    unit: 'U',
    isActive: true,
    isStockManaged: true,
    quantityAvailable: 0,
    imageUrl: null,
    ...overrides
  };
}

describe('ProductCardComponent rupture', () => {
  let fixture: ComponentFixture<ProductCardComponent>;
  let component: ProductCardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductCardComponent],
      providers: [
        {
          provide: ProductService,
          useValue: {
            resolveProductImageUrl: () => null,
            getProductImageUrl: () => of({ success: false, data: null })
          }
        },
        {
          provide: PosFavoritesService,
          useValue: {
            isFavorite: () => false,
            toggleFavorite: () => undefined,
            addRecent: () => undefined
          }
        },
        {
          provide: PosStateService,
          useValue: { isCreditNote: () => false }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductCardComponent);
    component = fixture.componentInstance;
  });

  it('does not emit addToOrder when the product is out of stock', fakeAsync(() => {
    component.product = product({ quantityAvailable: 0 });
    fixture.detectChanges();

    let emitted = false;
    component.addToOrder.subscribe(() => {
      emitted = true;
    });

    fixture.nativeElement.querySelector('.product-card').click();
    tick(300);

    expect(component.isAddDisabled()).toBeTrue();
    expect(emitted).toBeFalse();
  }));

  it('emits addToOrder when stock is available', fakeAsync(() => {
    component.product = product({ quantityAvailable: 4 });
    fixture.detectChanges();

    let emitted = false;
    component.addToOrder.subscribe(() => {
      emitted = true;
    });

    fixture.nativeElement.querySelector('.product-card').click();
    tick(300);

    expect(emitted).toBeTrue();
  }));
});
