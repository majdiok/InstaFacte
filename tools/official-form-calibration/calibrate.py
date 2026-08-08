"""
Calibration de la carte de coordonnées d'un formulaire officiel DGI.

Le gabarit fourni par la DGI est un PDF *plat* (aucun champ AcroForm) : les emplacements à
remplir y sont matérialisés par des suites de points « .......... ». Ce script extrait la boîte
englobante de chacune de ces suites, y associe le libellé arabe de sa ligne (colonne la plus à
droite — le formulaire est en RTL), et émet un fichier de *candidats* que l'humain n'a plus qu'à
nommer (attribution des clés fonctionnelles) avant de le figer dans `mensuelle-<millésime>.map.json`.

Le repère est identique à celui de PDFsharp `XGraphics` (origine haut-gauche, Y vers le bas, en
points) : aucune conversion n'est nécessaire entre ce script et le moteur de tamponnage.

Usage :
    python calibrate.py <gabarit.pdf> [--pages 1,4,5,6,7,8,9] [--out candidates.json]

Dépendance : pdfplumber (outil hors build, non embarqué dans l'application).
"""

from __future__ import annotations

import argparse
import json
import sys

try:
    import pdfplumber
except ImportError:  # pragma: no cover - outil de développement
    sys.exit("pdfplumber requis :  pip install pdfplumber")

# Écart mesuré (spike Lot 0) entre l'ordonnée de la ligne de base demandée à PDFsharp et le
# `bottom` que pdfplumber rapporte pour le texte ainsi dessiné.
BASELINE_FROM_BOTTOM = 1.9

# Une suite d'au moins 4 points est considérée comme un emplacement à remplir (en deçà, il
# s'agit de ponctuation ou d'un renvoi de note).
MIN_DOTS = 4


def row_label(page, box, *, tolerance: float = 4.0, max_chars: int = 70) -> str:
    """Libellé de la ligne : le texte situé à droite de l'emplacement (formulaire RTL)."""
    top, bottom = box["top"], box["bottom"]
    words = [
        w for w in page.extract_words()
        if w["x0"] >= box["x1"]
        and w["top"] < bottom + tolerance
        and w["bottom"] > top - tolerance
        and w["text"].count(".") < MIN_DOTS
    ]
    words.sort(key=lambda w: w["x0"])
    return " ".join(w["text"] for w in words)[:max_chars]


def collect(pdf_path: str, pages: list[int]) -> list[dict]:
    candidates: list[dict] = []

    with pdfplumber.open(pdf_path) as pdf:
        for page_no in pages:
            page = pdf.pages[page_no - 1]

            for word in page.extract_words():
                if word["text"].count(".") < MIN_DOTS:
                    continue

                candidates.append({
                    "key": f"TODO.p{page_no}.{len(candidates):03d}",
                    "page": page_no,
                    # Aligné à droite : la valeur se termine au bord droit de l'emplacement.
                    "x": round(word["x1"], 1),
                    "y": round(word["bottom"] - BASELINE_FROM_BOTTOM, 1),
                    "align": "right",
                    "fontSize": 8,
                    "label": row_label(page, word),
                    # Diagnostic (non consommé par l'application) : largeur utile de la case.
                    "_x0": round(word["x0"], 1),
                    "_width": round(word["x1"] - word["x0"], 1),
                })

    candidates.sort(key=lambda c: (c["page"], c["y"], c["x"]))
    return candidates


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("template", help="Gabarit PDF officiel vierge")
    parser.add_argument("--pages", default="1,4,5,6,7,8,9",
                        help="Pages à calibrer (1-based, séparées par des virgules)")
    parser.add_argument("--out", default="candidates.json")
    args = parser.parse_args()

    pages = [int(p) for p in args.pages.split(",") if p.strip()]
    candidates = collect(args.template, pages)

    with open(args.out, "w", encoding="utf-8") as handle:
        json.dump(candidates, handle, ensure_ascii=False, indent=2)

    print(f"{len(candidates)} emplacements détectés sur les pages {pages} -> {args.out}")
    for page_no in pages:
        count = sum(1 for c in candidates if c["page"] == page_no)
        print(f"  page {page_no:2d} : {count:3d}")


if __name__ == "__main__":
    main()
