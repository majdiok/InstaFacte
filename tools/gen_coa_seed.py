#!/usr/bin/env python3
"""Read the canonical NCT 01 catalog JSON.

New tenants receive the NCT plan via Nct01ChartMigrationService (embedded
nct01-coa-catalog.json), not via the historical AddAccountingModule_Tenant seed.
Do not paste generated rows into 20260326233329_AddAccountingModule_Tenant.cs.
"""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "docs" / "accounting" / "nct01-coa-catalog.json"


def main() -> None:
    data = json.loads(CATALOG.read_text(encoding="utf-8"))
    accounts = data["accounts"]
    numbers = {a["number"] for a in accounts}
    print(f"version={data.get('version')} accounts={len(accounts)} source={data.get('source')}")
    for required in ("603", "79", "7865", "43652", "4371", "4320", "641", "228"):
        status = "ok" if required in numbers else "MISSING"
        print(f"  {required}: {status}")
    if "4477" in numbers:
        raise SystemExit("Catalogue must not contain 4477 (FODEC overlay is 43652).")

    print()
    print("// Sample Row() lines for documentation only — do not seed 20260326.")
    for account in accounts[:8]:
        parent = "null" if not account.get("parent") else f'"{account["parent"]}"'
        print(
            f'            Row(Guid.NewGuid().ToString(), "{account["number"]}", '
            f'"{account["label"]}", {account["accountClass"]}, {parent}, '
            f'{account["natureType"]}, {account["level"]});'
        )


if __name__ == "__main__":
    main()
