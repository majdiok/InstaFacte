from pathlib import Path

mig = Path(__file__).resolve().parent.parent / "src/Backend/FactuTrust.Infrastructure/Migrations/Tenant/20260326233329_AddAccountingModule_Tenant.cs"
frag = Path(__file__).resolve().parent / "coa_seed_fragment.cs"
text = mig.read_text(encoding="utf-8")
insert = frag.read_text(encoding="utf-8")
needle = (
    '            Row("11111111-1111-1111-1111-111111111029", "131", '
    '"Résultat bénéficiaire", 1, null, 1, 3);\n'
)
if needle not in text:
    raise SystemExit("needle not found")
text = text.replace(needle, needle + insert)
mig.write_text(text, encoding="utf-8")
print("ok", insert.count("Row("), "rows")
