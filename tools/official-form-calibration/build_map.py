"""
Génère `mensuelle-2026.map.json` — la carte des coordonnées de tamponnage du formulaire
officiel « التصريح الشهري بالأداءات » (millésime 2026).

Les positions ci-dessous ne sont pas des nombres magiques : elles proviennent de `calibrate.py`,
qui lit la géométrie réelle du gabarit (boîtes des suites de points, grilles de tableaux). Ce
script les nomme — c'est l'étape humaine — et produit la carte consommée par
`OfficialFormStamper`.

Repère : origine haut-gauche, Y vers le bas, en points ; `y` désigne la LIGNE DE BASE du texte.
Identique à celui de PDFsharp `XGraphics` (vérifié par le spike Lot 0).

Usage :  python build_map.py [--out <chemin du .map.json>]
"""

from __future__ import annotations

import argparse
import json

# Écart mesuré entre la ligne de base demandée et le `bottom` rapporté par pdfplumber.
DY = 1.9

fields: list[dict] = []


def add(key: str, page: int, x: float, y: float, *, align: str = "right",
        size: float = 8.0, label: str = "") -> None:
    fields.append({
        "key": key, "page": page,
        "x": round(x, 1), "y": round(y, 1),
        "align": align, "fontSize": size, "label": label,
    })


def row(prefix: str, page: int, bottom: float, columns: dict[str, float], label: str) -> None:
    """Une ligne de tableau : plusieurs colonnes partageant la même ligne de base."""
    for suffix, x in columns.items():
        add(f"{prefix}.{suffix}", page, x, bottom - DY, label=f"{label} — {suffix}")


# ─────────────────────────────────────────────────────────────────────────────
# PAGE 1 — en-tête et identité
# ─────────────────────────────────────────────────────────────────────────────
# Année : 4 cases de 15 pt, y=(105.2, 118.4). Un champ par case (chiffre centré).
for i, x0 in enumerate([87.2, 102.2, 117.1, 132.1]):
    add(f"Header.Year.D{i + 1}", 1, x0 + 7.5, 116.5, align="center", size=9,
        label="السنة (année) — case %d" % (i + 1))

# Mois : 2 cases, y=(106.7, 119.9).
for i, x0 in enumerate([216.8, 230.5]):
    add(f"Header.Month.D{i + 1}", 1, x0 + 7.5, 118.0, align="center", size=9,
        label="الشهر (mois) — case %d" % (i + 1))

# Code de déclaration : 0 spontanée / 1 régularisation / 2 rectificative / 3 taxation d'office /
# 4 cessation d'activité (note (1) du formulaire).
add("Header.DeclarationCode", 1, 310.9, 118.0, align="center", size=9,
    label="رمز التصريح (code de déclaration)")

# Matricule fiscal : 8 cases (7 chiffres + lettre catégorie), remplies de DROITE à GAUCHE.
_nif_cells = [(332.0, 356.4), (313.3, 332.0), (292.2, 313.3), (271.6, 292.2),
              (250.0, 271.6), (230.6, 250.0), (209.6, 230.6), (185.4, 209.6)]
for i, (x0, x1) in enumerate(_nif_cells):
    add(f"Header.Nif.D{i + 1}", 1, (x0 + x1) / 2, 163.0, align="center", size=9,
        label="المعرف الجبائي — case %d (droite→gauche)" % (i + 1))

add("Header.VatCode", 1, 151.3, 163.0, align="center", size=9,
    label="رمز الأداء على القيمة المضافة (code TVA)")
add("Header.CategoryCode", 1, 83.4, 163.0, align="center", size=9,
    label="رمز الصنف (code catégorie)")

# Identité : libellés à droite (RTL) — la valeur se termine au bord droit de l'emplacement,
# donc juste à gauche du libellé.
add("Identity.Name", 1, 425.0, 183.1, label="الاسم واللقب أو الاسم الاجتماعي")
add("Identity.Address1", 1, 425.0, 196.1, label="العنوان أو المقر الاجتماعي")
add("Identity.Address2", 1, 540.0, 209.1, label="العنوان (suite) / الترقيم البريدي")
add("Identity.Activity", 1, 498.0, 226.1, label="النشاط")

# Cases à cocher « nature de l'impôt déclaré » (X).
# La rangée à remplir est celle SOUS les libellés (ceux-ci occupent y≈272-303) : elle se termine
# sur la bordure basse du tableau, à y=341.9. Centres de colonnes relevés sur les rectangles du
# gabarit, de droite à gauche (RTL).
for key, cx, lbl in [
    ("Withholding", 524.5, "الخصم من المورد"),
    ("Tfp", 477.2, "الأداء على التكوين المهني"),
    ("Foprolos", 414.2, "المساهمة في صندوق النهوض بالمسكن"),
    ("ConsumptionDuty", 351.7, "المعلوم على الاستهلاك"),
    ("Vat", 298.7, "الأداء على القيمة المضافة"),
    ("OtherDuties", 239.9, "معاليم أخرى على رقم المعاملات"),
    ("StampDuty", 185.0, "معلوم الطابع الجبائي"),
    ("Tcl", 134.7, "المعلوم على المؤسسات (TCL)"),
    ("HotelTax", 86.2, "المعلوم على النزل"),
    ("LicenceFee", 46.5, "معلوم الإجازة"),
]:
    add(f"Check.{key}", 1, cx, 330.0, align="center", size=10, label=f"Case à cocher — {lbl}")

# ─────────────────────────────────────────────────────────────────────────────
# PAGES 1-3 — الخصم من المورد : lignes réglementaires
# Toutes les lignes du tableau RS partagent la même géométrie de colonnes :
#   أساس الخصم (assiette) à droite, x1 = 306 ; مبلغ الخصم (montant) à gauche, x1 = 91.
# Les ordonnées proviennent du dénombrement des lignes de chaque page, recoupé avec la
# numérotation officielle des articles (1 à 31).
# ─────────────────────────────────────────────────────────────────────────────
WH_BASE_X, WH_AMOUNT_X = 306.0, 91.0

for key, page, bottom, lbl in [
    ("Line1", 1, 406.0, "1- المرتبات والأجور (القانون العام)"),
    ("Line3", 1, 461.1, "3- المساهمة الاجتماعية التضامنية على المرتبات والأجور"),
    ("Line4Individuals", 1, 514.0, "4- العمولات/الوساطة/الأكرية — أشخاص طبيعيون مقيمون 10%"),
    ("Line4Entities", 1, 529.0, "4- العمولات/الوساطة/الأكرية — أشخاص معنويون مقيمون 10%"),
    ("Line5", 1, 591.0, "5- الأتعاب — أشخاص طبيعيون غير خاضعين للنظام الحقيقي 10%"),
    ("Line6", 1, 619.0, "6- الأتعاب — أشخاص معنويون / النظام الحقيقي 3%"),
    ("Line8", 1, 674.0, "8- أكرية النزل 5%"),
    ("Line10", 2, 76.0, "10- فوائد الإيداعات والقروض 20%"),
    ("Line12", 2, 249.0, "12- حصص الأسهم — أشخاص طبيعيون مقيمون 10%"),
    ("Line16", 2, 507.0, "16- التفويت في العقارات — مقيمون 2,5%"),
    ("Line17Rate1", 2, 628.0, "17- اقتناءات ≥ 1000 د — مؤسسات خاضعة للضريبة 20% → 1%"),
    ("Line17Rate05", 2, 642.0, "17- اقتناءات ≥ 1000 د — مؤسسات خاضعة للضريبة 10% → 0,5%"),
    ("Line17Rate15", 2, 669.0, "17- اقتناءات ≥ 1000 د — مؤسسات أخرى → 1,5%"),
    ("Line18", 3, 91.0, "18- الخصم بعنوان أ.ق.م — الصفقات العمومية 25%"),
    ("Line19", 3, 136.0, "19- الخصم بعنوان أ.ق.م — غير المستقرين 100%"),
    ("Line24", 3, 441.0, "24- القيمة الزائدة — أشخاص طبيعيون غير مقيمين 10%"),
    ("Line25", 3, 500.0, "25- مكافآت أخرى لغير المقيمين — أشخاص طبيعيون 15%"),
]:
    row(f"Withholding.{key}", page, bottom,
        {"Base": WH_BASE_X, "Amount": WH_AMOUNT_X}, f"الخصم من المورد — {lbl}")

# ─────────────────────────────────────────────────────────────────────────────
# PAGE 4 — retenue à la source (total), TFP, FOPROLOS
# ─────────────────────────────────────────────────────────────────────────────
add("Withholding.Total", 4, 91.0, 208.0 - DY, label="الخصم من المورد — المجموع")

# TFP : deux lignes de taux exclusives (1 % industries manufacturières / 2 % autres activités).
row("Tfp.Manufacturing", 4, 327.0, {"Base": 302.0, "Amount": 183.0},
    "الأداء على التكوين المهني — الصناعات المعملية 1%")
row("Tfp.Other", 4, 343.0, {"Base": 302.0, "Amount": 183.0},
    "الأداء على التكوين المهني — الأنشطة الأخرى 2%")
add("Tfp.Total", 4, 183.0, 439.0 - DY, label="الأداء على التكوين المهني — المجموع")
add("Tfp.Remainder", 4, 270.0, 454.0 - DY, label="الأداء على التكوين المهني — الباقي (I)-(II)")

row("Foprolos", 4, 632.2, {"Base": 532.0, "Amount": 192.9},
    "المساهمة في صندوق النهوض بالمسكن لفائدة الأجراء 1%")

# ─────────────────────────────────────────────────────────────────────────────
# PAGE 5 — Taxe sur la valeur ajoutée
# Colonnes (RTL) : المبلغ/base à droite (x1≈338), أ.ق.م المستوجب au centre (x1≈205),
# أ.ق.م القابل للطرح à gauche (x1≈108).
# ─────────────────────────────────────────────────────────────────────────────
row("Vat.Rate7", 5, 279.6, {"Base": 336.4, "Due": 205.5}, "رقم المعاملات الخاضع 7%")
row("Vat.Rate13", 5, 292.4, {"Base": 338.5, "Due": 205.5}, "رقم المعاملات الخاضع 13%")
row("Vat.Rate19", 5, 305.0, {"Base": 338.5, "Due": 205.5}, "رقم المعاملات الخاضع 19%")

# §2 — achats ouvrant droit à déduction (base à droite, TVA déductible à gauche).
for key, bottom, lbl in [
    ("RealEstate", 397.0, "شراء عقارات"),
    ("EquipmentLocal", 422.0, "شراء معدات — محلية"),
    ("EquipmentImported", 436.0, "شراء معدات — موردة"),
    ("OtherLocal", 462.0, "شراءات أخرى — محلية"),
    ("OtherImported", 475.0, "شراءات أخرى — موردة"),
]:
    row(f"Vat.Purchase.{key}", 5, bottom, {"Base": 336.0, "Deductible": 108.0}, lbl)

add("Vat.Total.Due", 5, 204.0, 668.0 - DY, label="أ.ق.م — المجموع المستوجب (I)")
add("Vat.Total.Deductible", 5, 108.0, 668.0 - DY, label="أ.ق.م — المجموع القابل للطرح (II)")
add("Vat.Remainder", 5, 208.0, 696.0 - DY, label="الباقي: (ب) مستوجب أو (ف) فائض")
add("Vat.PreviousCredit", 5, 208.0, 711.0 - DY, label="الفائض من الشهر السابق")

# ─────────────────────────────────────────────────────────────────────────────
# PAGE 6 — معاليم أخرى : FODEC (المعلوم المهني 1 %)
# ─────────────────────────────────────────────────────────────────────────────
row("Fodec", 6, 478.0, {"Base": 230.0, "Amount": 102.0},
    "صندوق تنمية القدرة التنافسية — المعلوم المهني 1%")

# ─────────────────────────────────────────────────────────────────────────────
# PAGE 9 — خلاصة الأداءات والمعاليم الواجب دفعها (récapitulatif)
# 11 lignes régulières × 5 colonnes (RTL, de droite à gauche) :
#   Principal (I) → Déduction (II) → Dû (III) = I-II → Pénalités → Total
# ─────────────────────────────────────────────────────────────────────────────
RECAP_COLUMNS = {"Principal": 378.0, "Deduction": 304.0, "Due": 229.0,
                 "Penalties": 157.0, "Total": 91.0}

for key, bottom, lbl in [
    ("Withholding", 532.0, "الخصم من المورد"),
    ("Tfp", 549.0, "الأداء على التكوين المهني"),
    ("Foprolos", 571.0, "المساهمة في صندوق النهوض بالمسكن"),
    ("ConsumptionDuty", 595.0, "المعلوم على الاستهلاك"),
    ("Vat", 611.0, "الأداء على القيمة المضافة"),
    ("OtherDuties", 628.0, "معاليم أخرى"),
    ("StampDuty", 644.0, "معاليم الطابع الجبائي"),
    ("HotelTax", 678.0, "المعلوم على النزل"),
    ("Tcl", 702.0, "المعلوم على المؤسسات"),
    ("LicenceFee", 726.0, "معلوم الإجازة"),
    ("Total", 743.0, "المجموع"),
]:
    row(f"Recap.{key}", 9, bottom, RECAP_COLUMNS, f"خلاصة — {lbl}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--out",
        default="../../src/Backend/FactuTrust.Infrastructure/Resources/OfficialForms/mensuelle-2026.map.json")
    args = parser.parse_args()

    document = {
        "templateVersion": "2026",
        "templateFile": "mensuelle-2026.pdf",
        "pageCount": 12,
        "_generatedBy": "tools/official-form-calibration/build_map.py",
        "fields": fields,
    }

    with open(args.out, "w", encoding="utf-8") as handle:
        json.dump(document, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    per_page: dict[int, int] = {}
    for f in fields:
        per_page[f["page"]] = per_page.get(f["page"], 0) + 1
    print(f"{len(fields)} cases -> {args.out}")
    for page in sorted(per_page):
        print(f"  page {page:2d} : {per_page[page]:3d}")


if __name__ == "__main__":
    main()
