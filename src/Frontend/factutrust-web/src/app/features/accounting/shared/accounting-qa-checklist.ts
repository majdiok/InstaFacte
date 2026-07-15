/**
 * Manual QA checklist for accounting UI changes (viewports 1280, 1024, 375).
 * Run after each PR touching features/accounting/.
 */
export const ACCOUNTING_UI_QA_CHECKLIST = [
  'Journal: change period + Actualiser — same rows and debit/credit totals',
  'Balance: click account 4111 — navigates to /accounting/ledger with queryParams',
  'Ledger: Afficher — lines, running balance, footer totals',
  'VAT: Charger — amounts match API; draft/submit unchanged',
  'AI (ai:chat): payloadBuilder + assistant opens; screenIds unchanged',
  'Without ai:chat: IA button hidden',
  'Lettering: Charger + lettering workflow intact',
  'Ledger/Lettering: Afficher/Charger and IA buttons do not overlap at 1024–1280px',
  'AI screen analysis — income statement: 6 sections markdown + KPI dashboard',
  'AI screen analysis — ledger: équilibre débit/crédit mentionné, pas de copier/coller demandé',
  'AI screen analysis — cash-desk: avertissement pagination si plusieurs pages',
  'AI screen analysis — Trace chip shows screenId and payload size',
  'AI screen analysis — reload conversation: user bubble shows « Analyse de l\'écran : … » only',
  'AI free chat (no analysis): unchanged behavior, no ScreenAnalysis mode'
] as const;
