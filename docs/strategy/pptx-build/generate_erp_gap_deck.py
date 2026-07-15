#!/usr/bin/env python3
"""Generate FactuTrust ERP gap analysis presentation."""

from __future__ import annotations

from pathlib import Path

from pptx import Presentation
from pptx.chart.data import CategoryChartData
from pptx.dml.color import RGBColor
from pptx.enum.chart import XL_CHART_TYPE, XL_LEGEND_POSITION
from pptx.enum.text import MSO_ANCHOR, PP_ALIGN
from pptx.util import Inches, Pt

# Brand palette
NAVY = RGBColor(0x0B, 0x1F, 0x3A)
TEAL = RGBColor(0x0E, 0xA5, 0xA0)
TEAL_LIGHT = RGBColor(0xCC, 0xF0, 0xEE)
AMBER = RGBColor(0xF5, 0x9E, 0x0B)
SLATE = RGBColor(0x64, 0x74, 0x8B)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
OFF_WHITE = RGBColor(0xF8, 0xFA, 0xFC)
DARK_TEXT = RGBColor(0x1E, 0x29, 0x3B)
GREEN = RGBColor(0x10, 0xB9, 0x81)
RED = RGBColor(0xEF, 0x44, 0x44)

SLIDE_W = Inches(13.333)
SLIDE_H = Inches(7.5)
OUT_PATH = Path(__file__).resolve().parent.parent / "erp-gap-analysis.pptx"


def set_slide_bg(slide, color: RGBColor) -> None:
    fill = slide.background.fill
    fill.solid()
    fill.fore_color.rgb = color


def add_rect(slide, left, top, width, height, fill: RGBColor, line: RGBColor | None = None):
    shape = slide.shapes.add_shape(1, left, top, width, height)  # MSO_SHAPE.RECTANGLE
    shape.fill.solid()
    shape.fill.fore_color.rgb = fill
    if line:
        shape.line.color.rgb = line
    else:
        shape.line.fill.background()
    return shape


def add_textbox(slide, left, top, width, height, text, size=18, bold=False, color=DARK_TEXT, align=PP_ALIGN.LEFT):
    box = slide.shapes.add_textbox(left, top, width, height)
    tf = box.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = MSO_ANCHOR.TOP
    p = tf.paragraphs[0]
    p.text = text
    p.font.size = Pt(size)
    p.font.bold = bold
    p.font.color.rgb = color
    p.font.name = "Calibri"
    p.alignment = align
    return box


def add_bullets(slide, left, top, width, height, items, size=16, color=DARK_TEXT, spacing=6):
    box = slide.shapes.add_textbox(left, top, width, height)
    tf = box.text_frame
    tf.word_wrap = True
    for i, item in enumerate(items):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.text = item
        p.level = 0
        p.font.size = Pt(size)
        p.font.color.rgb = color
        p.font.name = "Calibri"
        p.space_after = Pt(spacing)
    return box


def add_header(slide, title: str, subtitle: str = "", dark: bool = False) -> None:
    bar_h = Inches(1.05)
    add_rect(slide, 0, 0, SLIDE_W, bar_h, NAVY if dark else TEAL)
    add_rect(slide, 0, bar_h, SLIDE_W, Inches(0.06), AMBER)
    tc = WHITE
    add_textbox(slide, Inches(0.55), Inches(0.18), Inches(10), Inches(0.5), title, 28, True, tc)
    if subtitle:
        add_textbox(slide, Inches(0.55), Inches(0.62), Inches(11), Inches(0.35), subtitle, 14, False, TEAL_LIGHT if not dark else RGBColor(0xCB, 0xD5, 0xE1))


def add_footer(slide, text: str = "FactuTrust — Analyse ERP — Mai 2026 — Confidentiel") -> None:
    add_textbox(slide, Inches(0.55), Inches(7.05), Inches(12), Inches(0.3), text, 10, False, SLATE, PP_ALIGN.LEFT)


def slide_title(prs: Presentation, title: str, subtitle: str, tagline: str):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, NAVY)
    add_rect(slide, 0, Inches(5.8), SLIDE_W, Inches(1.7), TEAL)
    add_textbox(slide, Inches(0.8), Inches(1.2), Inches(11), Inches(0.5), "FACTUTRUST", 16, True, TEAL)
    add_textbox(slide, Inches(0.8), Inches(1.85), Inches(11), Inches(1.4), title, 40, True, WHITE)
    add_textbox(slide, Inches(0.8), Inches(3.35), Inches(10.5), Inches(0.9), subtitle, 22, False, RGBColor(0xCB, 0xD5, 0xE1))
    add_textbox(slide, Inches(0.8), Inches(6.05), Inches(10), Inches(0.5), tagline, 14, False, NAVY)
    add_textbox(slide, Inches(0.8), Inches(6.55), Inches(10), Inches(0.35), "Comité Produit · 23 mai 2026", 12, False, NAVY)


def slide_exec_summary(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Synthèse exécutive", "FactuTrust = PGI avancé, pas encore ERP complet")
    cards = [
        ("3,0 / 5", "Maturité ERP\nPME commerce TN"),
        ("13", "Modules licenciés\nimplémentés"),
        ("~420 j/h", "Phase 1\nERP commercial TN"),
        ("~2 810 j/h", "ERP complet\nmid-market (24–36 mois)"),
    ]
    x0 = Inches(0.55)
    for i, (val, label) in enumerate(cards):
        left = x0 + i * Inches(3.15)
        add_rect(slide, left, Inches(1.45), Inches(2.95), Inches(1.55), WHITE, RGBColor(0xE2, 0xE8, 0xF0))
        add_textbox(slide, left + Inches(0.2), Inches(1.65), Inches(2.5), Inches(0.55), val, 30, True, TEAL, PP_ALIGN.CENTER)
        add_textbox(slide, left + Inches(0.15), Inches(2.25), Inches(2.6), Inches(0.65), label, 13, False, SLATE, PP_ALIGN.CENTER)
    add_bullets(
        slide,
        Inches(0.55),
        Inches(3.25),
        Inches(12.2),
        Inches(3.5),
        [
            "Couverture solide : vente → achat → stock → trésorerie → comptabilité + TEJ + IA + prévisions",
            "Positionnement recommandé : « ERP commercial intelligent Tunisie » avant ERP généraliste",
            "Gaps critiques P0 : TTN bout-en-bout, POS enterprise, listes de prix, lots/séries, API publique",
            "Gaps structurels : RH/paie, production/MRP, projets, immobilisations, consolidation groupe",
        ],
        17,
    )
    add_footer(slide)


def slide_maturity_chart(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Maturité ERP par segment", "Échelle 0–5 · Analyse codebase mai 2026")

    chart_data = CategoryChartData()
    chart_data.categories = [
        "PME commerce TN",
        "Distribution / grossiste",
        "Retail multi-magasins",
        "PME industrielle",
        "ESN / services",
        "Groupe multi-sociétés",
    ]
    chart_data.add_series("Maturité actuelle", (3.0, 2.5, 2.0, 1.2, 1.5, 1.0))
    chart_data.add_series("Cible Phase 1", (4.5, 4.0, 3.5, 1.5, 2.0, 1.5))

    chart = slide.shapes.add_chart(
        XL_CHART_TYPE.COLUMN_CLUSTERED,
        Inches(0.55),
        Inches(1.35),
        Inches(8.2),
        Inches(5.5),
        chart_data,
    ).chart
    chart.has_legend = True
    chart.legend.position = XL_LEGEND_POSITION.BOTTOM
    chart.legend.include_in_layout = False
    chart.value_axis.maximum_scale = 5
    chart.value_axis.minimum_scale = 0

    add_rect(slide, Inches(9.0), Inches(1.55), Inches(3.75), Inches(4.8), WHITE, RGBColor(0xE2, 0xE8, 0xF0))
    add_textbox(slide, Inches(9.25), Inches(1.75), Inches(3.3), Inches(0.4), "Lecture clé", 16, True, NAVY)
    add_bullets(
        slide,
        Inches(9.2),
        Inches(2.2),
        Inches(3.4),
        Inches(4.0),
        [
            "Fort sur le cycle commercial tunisien",
            "Phase 1 adresse 80 % des PME commerce",
            "Industrie & groupe = phases 3–4",
            "RH/paie = prérequis 50+ salariés",
        ],
        13,
        SLATE,
    )
    add_footer(slide)


def slide_modules_present(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Modules existants — socle solide", "13 AppModule + extensions hors catalogue")

    cols = [
        ("✅ Production", ["Clients & Produits", "Ventes (devis, BL, factures)", "Achats & PO", "Stock multi-entrepôts", "Trésorerie & banque", "Comptabilité (13 écrans)"]),
        ("✅ Différenciation", ["Fiscal TEJ natif", "CRM pipeline", "Rapports (18+ endpoints)", "Assistant IA + OCR", "Prévisions & réappro V2", "Backoffice SaaS"]),
        ("⚠️ Partiel / hors catalogue", ["POS (UI ok, API in-memory)", "Virtual Street e-commerce", "ChannelGateway (stub)", "Multi-émetteur léger", "Champs TTN (sans connecteur)", "Docs utilisateur incomplètes"]),
    ]
    for i, (title, items) in enumerate(cols):
        left = Inches(0.55) + i * Inches(4.15)
        fill = TEAL_LIGHT if i < 2 else RGBColor(0xFE, 0xF3, 0xC7)
        add_rect(slide, left, Inches(1.4), Inches(3.95), Inches(5.35), fill)
        add_textbox(slide, left + Inches(0.2), Inches(1.55), Inches(3.5), Inches(0.45), title, 15, True, NAVY)
        add_bullets(slide, left + Inches(0.2), Inches(2.05), Inches(3.55), Inches(4.5), [f"• {x}" for x in items], 13, DARK_TEXT, 4)
    add_footer(slide)


def slide_gaps_overview(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Modules ERP absents ou incomplets", "Écart vs référentiel Gartner / Odoo Enterprise")

    rows = [
        ("RH & Paie (HCM)", "❌ Absent", "~515 j/h", "Phase 4"),
        ("Production & MRP", "❌ Absent", "~415 j/h", "Phase 3"),
        ("Projets & PSA", "❌ Absent", "~280 j/h", "Phase 2"),
        ("Immobilisations", "❌ Absent", "~180 j/h", "Phase 4"),
        ("WMS avancé (lots, WMS)", "⚠️ Partiel", "~345 j/h", "Phase 1"),
        ("BI & consolidation", "⚠️ Partiel", "~355 j/h", "Phase 2–4"),
        ("Intégrations / API publique", "⚠️ Partiel", "~555 j/h", "Phase 1"),
    ]

    y = Inches(1.45)
    add_rect(slide, Inches(0.55), y, Inches(12.2), Inches(0.45), NAVY)
    for j, h in enumerate(["Domaine", "Statut", "Effort gap", "Phase cible"]):
        add_textbox(slide, Inches(0.7) + j * Inches(3.0), y + Inches(0.08), Inches(2.8), Inches(0.35), h, 12, True, WHITE)

    for i, (domain, status, effort, phase) in enumerate(rows):
        ry = y + Inches(0.5) + i * Inches(0.72)
        bg = WHITE if i % 2 == 0 else RGBColor(0xF1, 0xF5, 0xF9)
        add_rect(slide, Inches(0.55), ry, Inches(12.2), Inches(0.65), bg)
        add_textbox(slide, Inches(0.7), ry + Inches(0.12), Inches(2.8), Inches(0.4), domain, 13, True, NAVY)
        add_textbox(slide, Inches(3.7), ry + Inches(0.12), Inches(2.5), Inches(0.4), status, 12, False, DARK_TEXT)
        add_textbox(slide, Inches(6.7), ry + Inches(0.12), Inches(2.2), Inches(0.4), effort, 12, False, TEAL)
        add_textbox(slide, Inches(9.7), ry + Inches(0.12), Inches(2.5), Inches(0.4), phase, 12, True, DARK_TEXT)

    add_footer(slide)


def slide_phase1_p0(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Phase 1 — Priorités P0/P1 immédiates", "ERP commercial Tunisie · 6–9 mois · ~420 j/h (P0/P1)")

    lots = [
        ("L1 Conformité TN", "155 j/h", ["TTN / El Fatoora bout-en-bout", "Archivage probatoire 10 ans", "Import factures achat TTN"]),
        ("L2 Retail", "110 j/h", ["POS enterprise (caisses, clôtures Z)", "Persistance EF/Redis", "Conformité caisse TN"]),
        ("L3 Catalogue", "150 j/h", ["Listes de prix par client", "Remises & variantes produit", "Références multi-fournisseurs"]),
        ("L4 Stock pro", "130 j/h", ["Traçabilité lots / séries", "Dates péremption", "Codes-barres & scan"]),
        ("L5 Pilotage", "165 j/h", ["Comptabilité analytique", "Budgets & cash flow", "Plafonds crédit client"]),
        ("L6 Plateforme", "230 j/h", ["API publique v1 + webhooks", "Workflow approbations", "Portail client"]),
    ]

    for i, (title, effort, items) in enumerate(lots):
        col = i % 3
        row = i // 3
        left = Inches(0.55) + col * Inches(4.15)
        top = Inches(1.4) + row * Inches(2.85)
        add_rect(slide, left, top, Inches(3.95), Inches(2.65), WHITE, RGBColor(0xE2, 0xE8, 0xF0))
        add_textbox(slide, left + Inches(0.15), top + Inches(0.12), Inches(2.5), Inches(0.35), title, 14, True, TEAL)
        add_textbox(slide, left + Inches(2.6), top + Inches(0.12), Inches(1.2), Inches(0.35), effort, 12, True, AMBER, PP_ALIGN.RIGHT)
        add_bullets(slide, left + Inches(0.15), top + Inches(0.5), Inches(3.65), Inches(2.0), [f"• {x}" for x in items], 12, DARK_TEXT, 3)
    add_footer(slide)


def slide_roadmap_chart(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Roadmap par phases", "Effort cumulé ~2 810 j/h · équipe 4–6 devs senior")

    chart_data = CategoryChartData()
    chart_data.categories = ["Phase 1\nCommerce TN", "Phase 2\nServices", "Phase 3\nIndustrie", "Phase 4\nGroupe & RH"]
    chart_data.add_series("Effort (j/h)", (420, 680, 415, 695))

    chart = slide.shapes.add_chart(
        XL_CHART_TYPE.COLUMN_CLUSTERED,
        Inches(0.55),
        Inches(1.35),
        Inches(7.5),
        Inches(5.4),
        chart_data,
    ).chart
    chart.has_legend = False
    chart.plots[0].series[0].format.fill.solid()
    chart.plots[0].series[0].format.fill.fore_color.rgb = TEAL

    phases = [
        ("Phase 1 · 6–9 mois", "TTN, POS, tarifs, lots, analytique, API, portail"),
        ("Phase 2 · 9–12 mois", "Projets, CRM 360, GED, multi-devises, notes de frais"),
        ("Phase 3 · 12–18 mois", "BOM, ordres de fabrication, MRP, coûts, qualité"),
        ("Phase 4 · 18–36 mois", "RH/paie TN, immobilisations, consolidation, BI avancée"),
    ]
    for i, (title, desc) in enumerate(phases):
        top = Inches(1.5) + i * Inches(1.25)
        add_rect(slide, Inches(8.35), top, Inches(4.4), Inches(1.05), WHITE, RGBColor(0xE2, 0xE8, 0xF0))
        add_textbox(slide, Inches(8.5), top + Inches(0.1), Inches(4.1), Inches(0.35), title, 13, True, NAVY)
        add_textbox(slide, Inches(8.5), top + Inches(0.45), Inches(4.1), Inches(0.5), desc, 11, False, SLATE)
    add_footer(slide)


def slide_benchmark(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Benchmark concurrentiel", "PME commerce TN · 5–50 utilisateurs")

    headers = ["Capacité", "FactuTrust", "Odoo 17", "Sage", "Dynamics BC"]
    rows = [
        ["E-facture / TEJ TN", "◐ / ● ★", "○ / ○", "◐ / ◐", "○ / ○"],
        ["Comptabilité générale", "●", "●", "●", "●"],
        ["Compta analytique", "○", "●", "●", "●"],
        ["Stock & achats", "●", "●", "●", "●"],
        ["Production / MRP", "○", "●", "◐", "●"],
        ["RH & Paie TN", "○", "◐", "●", "◐"],
        ["IA & prévisions", "● ★", "◐", "○", "◐"],
        ["SaaS multi-tenant", "● ★", "◐", "○", "◐"],
        ["Time-to-value TN", "★ Rapide", "Long", "Moyen", "Long"],
    ]

    y0 = Inches(1.35)
    col_w = [Inches(3.2), Inches(2.0), Inches(2.0), Inches(2.0), Inches(2.0)]
    x = Inches(0.55)
    add_rect(slide, x, y0, sum(col_w), Inches(0.42), NAVY)
    for j, h in enumerate(headers):
        add_textbox(slide, x + Inches(0.05), y0 + Inches(0.07), col_w[j], Inches(0.3), h, 11, True, WHITE, PP_ALIGN.CENTER if j else PP_ALIGN.LEFT)
        x += col_w[j]

    for i, row in enumerate(rows):
        y = y0 + Inches(0.45) + i * Inches(0.55)
        bg = WHITE if i % 2 == 0 else RGBColor(0xF1, 0xF5, 0xF9)
        add_rect(slide, Inches(0.55), y, sum(col_w), Inches(0.52), bg)
        x = Inches(0.55)
        for j, cell in enumerate(row):
            color = TEAL if j == 1 and "★" in cell else DARK_TEXT
            bold = j == 0 or (j == 1 and "★" in cell)
            add_textbox(slide, x + Inches(0.05), y + Inches(0.1), col_w[j], Inches(0.35), cell, 10, bold, color, PP_ALIGN.CENTER if j else PP_ALIGN.LEFT)
            x += col_w[j]

    add_textbox(slide, Inches(0.55), Inches(6.55), Inches(12), Inches(0.35), "★ = avantage différenciant FactuTrust · ● complet · ◐ partiel · ○ absent", 10, False, SLATE)
    add_footer(slide)


def slide_advantages(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Avantages compétitifs FactuTrust", "Message produit cible")

    add_rect(slide, Inches(0.55), Inches(1.4), Inches(12.2), Inches(1.1), TEAL)
    add_textbox(
        slide,
        Inches(0.8),
        Inches(1.55),
        Inches(11.6),
        Inches(0.85),
        "« L'ERP intelligent des entreprises commerciales tunisiennes — conforme, unifié, assisté par IA. »",
        20,
        True,
        WHITE,
        PP_ALIGN.CENTER,
    )

    blocks = [
        ("vs Odoo", ["TEJ natif · IA/prévisions · SaaS TN-first · UX premium", "Gap : MRP, paie, marketing, profondeur modules"]),
        ("vs Sage", ["Modernité SaaS · cycle commercial unifié · coût abonnement", "Gap : paie certifiée, liasse fiscale, immobilisations"]),
        ("vs Dynamics BC", ["Simplicité PME · spécificités TN · time-to-value", "Gap : consolidation, écosystème Microsoft, manufacturing"]),
    ]
    for i, (title, items) in enumerate(blocks):
        left = Inches(0.55) + i * Inches(4.15)
        add_rect(slide, left, Inches(2.75), Inches(3.95), Inches(3.55), WHITE, RGBColor(0xE2, 0xE8, 0xF0))
        add_textbox(slide, left + Inches(0.2), Inches(2.9), Inches(3.5), Inches(0.4), title, 16, True, NAVY)
        add_bullets(slide, left + Inches(0.2), Inches(3.35), Inches(3.55), Inches(2.8), [f"• {x}" for x in items], 13, DARK_TEXT, 6)

    add_footer(slide)


def slide_segments(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Matrice de décision par segment", "FactuTrust suffit-il aujourd'hui ?")

    data = [
        ("TPE commerce / facturation", "✅ Oui", "TTN complet", "Phase 1"),
        ("PME distribution / grossiste", "⚠️ Presque", "Lots, tarifs, POS, codes-barres", "Phase 1"),
        ("Retail multi-magasins", "⚠️ Partiel", "POS enterprise, WMS light", "Phase 1–2"),
        ("PME industrielle", "❌ Non", "MRP, BOM, OF, coûts", "Phase 3"),
        ("ESN / BTP / conseil", "❌ Non", "Projets, temps, régie", "Phase 2"),
        ("Groupe multi-sociétés", "❌ Non", "Consolidation, interco, RH", "Phase 4"),
        ("Entreprise 50+ salariés", "❌ Non", "RH & paie TN", "Phase 4"),
    ]

    y = Inches(1.4)
    headers = ["Segment", "Aujourd'hui", "Gap bloquant", "Phase"]
    widths = [Inches(3.5), Inches(1.8), Inches(4.5), Inches(1.8)]
    x = Inches(0.55)
    add_rect(slide, x, y, sum(widths), Inches(0.42), NAVY)
    for j, h in enumerate(headers):
        add_textbox(slide, x + Inches(0.08), y + Inches(0.07), widths[j], Inches(0.3), h, 11, True, WHITE)
        x += widths[j]

    for i, row in enumerate(data):
        ry = y + Inches(0.45) + i * Inches(0.72)
        bg = WHITE if i % 2 == 0 else RGBColor(0xF1, 0xF5, 0xF9)
        add_rect(slide, Inches(0.55), ry, sum(widths), Inches(0.65), bg)
        x = Inches(0.55)
        colors = [DARK_TEXT, GREEN if "✅" in row[1] else AMBER if "⚠️" in row[1] else RED, SLATE, TEAL]
        for j, cell in enumerate(row):
            add_textbox(slide, x + Inches(0.08), ry + Inches(0.14), widths[j], Inches(0.4), cell, 12, j == 1, colors[j])
            x += widths[j]
    add_footer(slide)


def slide_technical_debt(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Dette produit à traiter en priorité", "Éléments partiellement implémentés dans le codebase")

    items = [
        ("POS session in-memory", "PosSessionController.cs", "Persistance EF/Redis + modèle caisse"),
        ("ChannelGateway", "src/ChannelGateway/", "Source Node + webhooks WhatsApp/Telegram"),
        ("MultiLevelApproval réappro", "ForecastingOptions.cs", "Activer flag + UI workflow"),
        ("Email facture fournisseur", "CreateSupplierInvoiceFromPOCommand", "TODO ligne 139"),
        ("Documentation utilisateur", "docs/utilisateur/", "Chapitres compta, CRM, IA, TEJ, POS"),
        ("Tests Domain/Application", "Solution backend", "Projets de tests manquants"),
    ]

    for i, (item, loc, action) in enumerate(items):
        top = Inches(1.45) + i * Inches(0.88)
        add_rect(slide, Inches(0.55), top, Inches(12.2), Inches(0.78), WHITE if i % 2 == 0 else RGBColor(0xF1, 0xF5, 0xF9), RGBColor(0xE2, 0xE8, 0xF0))
        add_textbox(slide, Inches(0.7), top + Inches(0.08), Inches(2.8), Inches(0.3), item, 13, True, NAVY)
        add_textbox(slide, Inches(3.6), top + Inches(0.08), Inches(3.5), Inches(0.3), loc, 11, False, SLATE)
        add_textbox(slide, Inches(7.2), top + Inches(0.08), Inches(5.3), Inches(0.55), action, 12, False, TEAL)
    add_footer(slide)


def slide_recommendations(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, OFF_WHITE)
    add_header(slide, "Recommandations & prochaines étapes", "Décisions comité produit")

    steps = [
        ("1", "Valider le segment cible Phase 1", "PME commerce & distribution Tunisie"),
        ("2", "Prioriser le lot TTN", "Soumission, statuts, archivage — différenciateur n°1"),
        ("3", "Lancer API publique v1", "Prérequis écosystème & intégrateurs"),
        ("4", "Finaliser POS & lots/séries", "Retail & distribution prêts à vendre"),
        ("5", "Roadmap formelle", "Transformer l'analyse en epics Jira/Linear"),
    ]

    for i, (num, title, desc) in enumerate(steps):
        top = Inches(1.45) + i * Inches(1.05)
        add_rect(slide, Inches(0.55), top, Inches(0.65), Inches(0.65), TEAL)
        add_textbox(slide, Inches(0.55), top + Inches(0.12), Inches(0.65), Inches(0.4), num, 18, True, WHITE, PP_ALIGN.CENTER)
        add_textbox(slide, Inches(1.4), top + Inches(0.05), Inches(5), Inches(0.35), title, 16, True, NAVY)
        add_textbox(slide, Inches(1.4), top + Inches(0.4), Inches(10.5), Inches(0.35), desc, 13, False, SLATE)

    add_rect(slide, Inches(0.55), Inches(6.35), Inches(12.2), Inches(0.55), NAVY)
    add_textbox(
        slide,
        Inches(0.8),
        Inches(6.45),
        Inches(11.6),
        Inches(0.35),
        "Option recommandée : ERP commercial intelligent Tunisie (Phase 1) avant ERP généraliste",
        14,
        True,
        WHITE,
        PP_ALIGN.CENTER,
    )
    add_footer(slide)


def slide_closing(prs: Presentation):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    set_slide_bg(slide, NAVY)
    add_textbox(slide, Inches(0.8), Inches(2.2), Inches(11), Inches(1), "Merci", 44, True, WHITE, PP_ALIGN.CENTER)
    add_textbox(
        slide,
        Inches(1.2),
        Inches(3.4),
        Inches(10.5),
        Inches(1.2),
        "Document détaillé : docs/strategy/erp-gap-analysis.md\nQuestions & arbitrages : Comité Produit FactuTrust",
        18,
        False,
        RGBColor(0xCB, 0xD5, 0xE1),
        PP_ALIGN.CENTER,
    )
    add_rect(slide, Inches(4.5), Inches(5.2), Inches(4.3), Inches(0.06), AMBER)


def build() -> Path:
    prs = Presentation()
    prs.slide_width = SLIDE_W
    prs.slide_height = SLIDE_H

    slide_title(
        prs,
        "Analyse des écarts ERP",
        "Modules & fonctionnalités pour devenir un ERP crédible",
        "De la gestion commerciale avancée à l'ERP intelligent des PME tunisiennes",
    )
    slide_exec_summary(prs)
    slide_maturity_chart(prs)
    slide_modules_present(prs)
    slide_gaps_overview(prs)
    slide_phase1_p0(prs)
    slide_roadmap_chart(prs)
    slide_benchmark(prs)
    slide_advantages(prs)
    slide_segments(prs)
    slide_technical_debt(prs)
    slide_recommendations(prs)
    slide_closing(prs)

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    prs.save(str(OUT_PATH))
    return OUT_PATH


if __name__ == "__main__":
    path = build()
    print(f"Generated: {path}")
