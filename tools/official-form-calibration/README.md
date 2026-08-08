# Calibration des formulaires officiels DGI

Outils **hors build** servant à produire la carte de coordonnées (`*.map.json`) utilisée par
`OfficialFormStamper` pour tamponner les formulaires officiels préimprimés.

## Pourquoi une carte de coordonnées

Le gabarit publié par la DGI est un PDF **plat** : aucun champ AcroForm, les emplacements à
remplir ne sont que des suites de points imprimées. Les valeurs doivent donc être dessinées à des
coordonnées absolues.

Ces coordonnées vivent dans un JSON versionné plutôt que dans le code C# : un nouveau millésime
se traite en remplaçant le couple (gabarit PDF, carte JSON), **sans modification C#**, et la carte
reste relisible par un fiscaliste.

## Repère

Origine **haut-gauche**, Y vers le bas, en **points** — identique à celui de `XGraphics`
(PDFsharp) et de `pdfplumber`. Aucune conversion d'axe n'est nécessaire. `y` désigne la **ligne de
base** du texte, pas le haut de la cellule.

## Procédure pour un nouveau millésime

```bash
pip install pdfplumber

# 1. Déposer le gabarit vierge
cp mensuelle2027.pdf ../../src/Backend/FactuTrust.Infrastructure/Resources/OfficialForms/mensuelle-2027.pdf

# 2. Détecter les emplacements (suites de points) et leur libellé de ligne
python calibrate.py ../../src/Backend/FactuTrust.Infrastructure/Resources/OfficialForms/mensuelle-2027.pdf

# 3. Reporter les positions dans build_map.py (étape humaine : nommage des clés), puis générer
python build_map.py --out ../../src/Backend/FactuTrust.Infrastructure/Resources/OfficialForms/mensuelle-2027.map.json
```

## Relecture visuelle (indispensable)

Les tests automatisés vérifient la cohérence de la carte et le placement au point près, mais
**seul l'œil humain valide qu'une valeur tombe dans la bonne case fiscale**. Pour produire une
épreuve remplie de valeurs témoins :

1. Écrire un test jetable qui appelle `OfficialFormStamper.Stamp` avec toutes les clés de la carte
   renseignées, et écrit le PDF sur disque.
2. Le rendre en image et le comparer au gabarit vierge :

```bash
python -c "import pdfplumber; pdfplumber.open('dump.pdf').pages[8].to_image(resolution=150).save('p9.png')"
```

Pièges rencontrés lors de la calibration 2026, à re-vérifier à chaque millésime :

- Les rangées à cocher sont **sous** leurs libellés — viser la bordure basse du tableau, pas la
  bande de texte.
- Le formulaire est en RTL : le libellé d'une ligne est la colonne la **plus à droite**, et les
  valeurs se lisent de droite à gauche (cases du matricule fiscal notamment).
- Ne jamais valider par recherche de sous-chaîne dans le texte extrait : les points du gabarit se
  mélangent aux valeurs tamponnées. Vérifier par position de caractère.
