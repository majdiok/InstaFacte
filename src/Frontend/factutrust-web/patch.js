const fs = require('fs');
const glob = require('glob');

function patchFile(filepath) {
    if (!fs.existsSync(filepath)) return;
    let content = fs.readFileSync(filepath, 'utf8');

    // Check if initialLoad is already present
    if (content.includes('initialLoad = signal(true)')) {
        console.log(`Skipping ${filepath}, already patched.`);
        return;
    }

    // 1. replace template @if (loading()) before app-skeleton-table
    // The exact string might vary, but usually looks like: `@if (loading()) {\n  <app-skeleton-table`
    // Wait, sometimes it's `loading` from signal, sometimes `loading` from component property.
    let updated = content.replace(/@if\s*\(\s*loading\(\)\s*\)\s*\{\s*(?=[^<]*<app-skeleton-table)/g, '@if (initialLoad()) {\n      ');
    // For non-signal loading property:
    updated = updated.replace(/@if\s*\(\s*loading\s*\)\s*\{\s*(?=[^<]*<app-skeleton-table)/g, '@if (initialLoad()) {\n      ');
    // Also, stock-list.component.html:
    updated = updated.replace(/\*ngIf="loading\(\)"(?=[^>]*><app-skeleton-table)/g, '*ngIf="initialLoad()"');

    // Make sure p-table has [loading]="loading()"
    if (!updated.includes('[loading]="loading()"') && updated.includes('<p-table')) {
        updated = updated.replace(/<p-table/g, '<p-table \n          [loading]="loading()"');
    }

    // 2. Add initialLoad signal
    updated = updated.replace(/loading\s*=\s*signal\(\s*(false|true)\s*\)\s*;/g, 'loading = signal(true);\n  initialLoad = signal(true);');

    // 3. Reset initialLoad
    updated = updated.replace(/this\.loading\.set\(\s*false\s*\)\s*;/g, 'this.loading.set(false);\n        this.initialLoad.set(false);');

    // Save if changed
    if (updated !== content) {
        fs.writeFileSync(filepath, updated);
        console.log(`Patched ${filepath}`);
    } else {
        console.log(`No changes made to ${filepath}`);
    }
}

const filesToPatch = [
    'src/app/features/suppliers/supplier-list/supplier-list.component.ts',
    'src/app/features/supplier-invoices/supplier-invoice-list/supplier-invoice-list.component.ts',
    'src/app/features/stock/stock-list/stock-list.component.ts',
    'src/app/features/stock/stock-list/stock-list.component.html',
    'src/app/features/stock/stock-history/stock-history.component.ts',
    'src/app/features/quotes/quote-list/quote-list.component.ts',
    'src/app/features/purchase-orders/purchase-order-list/purchase-order-list.component.ts',
    'src/app/features/payments/payments.component.ts',
    'src/app/features/invoices/invoice-list/invoice-list.component.ts',
    'src/app/features/dashboard/dashboard.component.ts',
    'src/app/features/clients/client-list/client-list.component.ts',
    'src/app/features/clients/client-detail/client-detail.component.ts'
];

filesToPatch.forEach(f => {
    patchFile(`c:/Mes Projets/FactuTrust - Copy/src/Frontend/factutrust-web/${f}`);
});
