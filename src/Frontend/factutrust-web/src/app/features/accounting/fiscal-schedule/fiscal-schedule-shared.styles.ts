/** Shared Sage-like styles for fiscal schedule subcomponents. */
export const FISCAL_SCHEDULE_SHARED_STYLES = `
  .icon-button,
  .square-button,
  .quick-action,
  .link-row,
  .summary-tile {
    border: 1px solid #d8dee8;
    background: #fff;
    color: #263241;
    border-radius: 6px;
    cursor: pointer;
    min-height: 34px;
    transition: border-color .15s ease, background .15s ease;
  }

  .icon-button {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    padding: 0 12px;
    font-size: 13px;
    font-weight: 600;
  }

  .icon-button.primary {
    background: #0f8f4d;
    color: #fff;
    border-color: #0f8f4d;
  }

  .icon-button.danger { color: #b42318; }
  .icon-button.apply { align-self: end; background: #f8fafc; }

  .icon-button:disabled,
  .quick-action:disabled,
  .square-button:disabled {
    opacity: .45;
    cursor: not-allowed;
  }

  .square-button {
    display: inline-grid;
    place-items: center;
    width: 34px;
    height: 34px;
  }

  label {
    display: grid;
    gap: 5px;
    font-size: 12px;
    font-weight: 700;
    color: #374151;
  }

  input, select, textarea {
    width: 100%;
    min-height: 34px;
    border: 1px solid #cfd7e3;
    border-radius: 5px;
    padding: 7px 9px;
    font: inherit;
    background: #fff;
    color: #1f2937;
  }

  .status-pill {
    display: inline-flex;
    align-items: center;
    min-height: 22px;
    padding: 2px 8px;
    border-radius: 5px;
    border: 1px solid currentColor;
    font-size: 11px;
    font-weight: 800;
  }

  .status-overdue { color: #dc2626; background: #fff1f0; }
  .status-soon { color: #b45309; background: #fff8e8; }
  .status-upcoming { color: #2563eb; background: #eff7ff; }
  .status-deposited { color: #15803d; background: #f0fdf4; }
  .status-validated { color: #047857; background: #ecfdf5; }
  .status-done { color: #047857; background: #ecfdf5; }
  .status-cancelled,
  .status-neutral { color: #64748b; background: #f8fafc; }

  .muted {
    color: #64748b;
    font-size: 12px;
    line-height: 1.45;
  }

  .amount-col {
    text-align: right;
    font-variant-numeric: tabular-nums;
  }
`;
