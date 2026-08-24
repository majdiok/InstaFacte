import { ProductFormComponent } from './product-form.component';

describe('ProductFormComponent variants', () => {
  it('previewCombinationCount multiplies selected axis values', () => {
    const component = Object.create(ProductFormComponent.prototype) as ProductFormComponent;
    (component as unknown as { variantAxesSelection: () => Record<string, string[]> }).variantAxesSelection = () => ({
      size: ['s1', 's2'],
      color: ['c1', 'c2', 'c3']
    });

    expect(component.previewCombinationCount()).toBe(6);
  });

  it('previewCombinationCount returns 0 when no selection', () => {
    const component = Object.create(ProductFormComponent.prototype) as ProductFormComponent;
    (component as unknown as { variantAxesSelection: () => Record<string, string[]> }).variantAxesSelection = () => ({});

    expect(component.previewCombinationCount()).toBe(0);
  });
});
