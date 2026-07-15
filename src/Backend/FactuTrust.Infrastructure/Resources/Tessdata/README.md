# Tesseract trained data files (Tessdata)

Ce dossier doit contenir les fichiers `*.traineddata` utilisés par Tesseract OCR
pour reconnaître le texte dans les PDF scannés et les pièces jointes images de
l'Assistant IA.

## Fichiers attendus

Téléchargez les modèles `tessdata_fast` (version rapide, ~10 Mo par langue) depuis :
https://github.com/tesseract-ocr/tessdata_fast

Fichiers requis (au minimum) :

- `fra.traineddata` — français
- `eng.traineddata` — anglais

Optionnels :

- `ara.traineddata` — arabe (utile pour factures bilingues TN)

Pour une qualité supérieure (factures complexes, texte mince), utiliser plutôt
`tessdata_best` (~30 Mo par langue) : https://github.com/tesseract-ocr/tessdata_best

## Comportement sans tessdata

Si ce dossier ne contient aucun fichier `.traineddata`, le service `TesseractOcrService`
sera automatiquement marqué `IsAvailable = false` et l'application continue de
fonctionner — seules les fonctions OCR (PDF scannés, images jointes) seront
désactivées. Le pipeline d'extraction PDF avec texte natif (la majorité des cas)
reste pleinement opérationnel.

## Déploiement

Les fichiers de ce dossier sont copiés automatiquement à côté du binaire de l'API
(`bin/{Configuration}/net8.0/Resources/Tessdata/`) via la directive `<None Include="...">`
du projet `FactuTrust.Infrastructure.csproj`.

En production Docker, s'assurer que ces fichiers sont inclus dans l'image finale.
