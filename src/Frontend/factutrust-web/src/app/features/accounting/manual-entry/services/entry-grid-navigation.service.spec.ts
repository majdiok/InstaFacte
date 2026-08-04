import { EntryGridNavigationService } from './entry-grid-navigation.service';

describe('EntryGridNavigationService', () => {
  let service: EntryGridNavigationService;
  let container: HTMLElement;

  beforeEach(() => {
    service = new EntryGridNavigationService();
    container = document.createElement('div');
    container.innerHTML = `
      <div data-row="0" data-field="account"><input type="text" /></div>
      <div data-row="0" data-field="debit"><app-accounting-amount-input><input type="text" /></app-accounting-amount-input></div>
      <div data-row="0" data-field="credit"><app-accounting-amount-input><input type="text" /></app-accounting-amount-input></div>
      <div data-row="1" data-field="account"><input type="text" /></div>
      <div data-row="1" data-field="debit"><app-accounting-amount-input><input type="text" /></app-accounting-amount-input></div>
      <div data-row="1" data-field="credit"><app-accounting-amount-input><input type="text" /></app-accounting-amount-input></div>
    `;
    document.body.appendChild(container);
  });

  afterEach(() => {
    container.remove();
  });

  it('focuses credit when tabbing from empty debit', () => {
    const focused = service.handleAmountTab({
      container,
      rowIndex: 0,
      field: 'debit',
      debit: null,
      credit: null,
      totalRows: 2
    }, false);

    expect(focused).toBe(true);
    const creditInput = container.querySelector('[data-field="credit"] input') as HTMLInputElement;
    expect(document.activeElement).toBe(creditInput);
  });

  it('skips credit and focuses next account when debit is filled', () => {
    const focused = service.handleAmountTab({
      container,
      rowIndex: 0,
      field: 'debit',
      debit: 100,
      credit: null,
      totalRows: 2
    }, false);

    expect(focused).toBe(true);
    const accountInput = container.querySelector('[data-row="1"][data-field="account"] input') as HTMLInputElement;
    expect(document.activeElement).toBe(accountInput);
  });

  it('focuses previous debit on shift+tab from credit', () => {
    const debitInput = container.querySelector('[data-row="0"][data-field="debit"] input') as HTMLInputElement;
    debitInput.focus();

    const focused = service.handleAmountTab({
      container,
      rowIndex: 0,
      field: 'credit',
      debit: null,
      credit: 50,
      totalRows: 2
    }, true);

    expect(focused).toBe(true);
    expect(document.activeElement).toBe(debitInput);
  });

  it('calls onAddLine when tabbing from last row credit', () => {
    const addLine = jasmine.createSpy('onAddLine');
    service.handleAmountTab({
      container,
      rowIndex: 1,
      field: 'credit',
      debit: null,
      credit: 100,
      totalRows: 2,
      onAddLine: addLine
    }, false);

    expect(addLine).toHaveBeenCalled();
  });
});
