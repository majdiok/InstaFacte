# Partenariat expert-comptable — pipeline vidéo

Génération de la vidéo commerciale `factutrust-partenariat-expert-comptable.mp4`.

## Prérequis

- Backend FactuTrust (port 7001) et frontend (port 4200)
- Node.js + dépendances frontend (`npm install`)
- Playwright Chromium (`npm run install:playwright`)
- Python + `edge-tts` (`pip install edge-tts`)
- ffmpeg dans le PATH

## Commandes (depuis `src/Frontend/factutrust-web`)

```powershell
# 1. Enregistrement screencast (8 chapitres)
npm run docs:partnership-video

# 2. Voix off TTS
..\..\..\docs\partnership\scripts\generate-tts.ps1

# 3. Assemblage MP4 final
..\..\..\docs\partnership\scripts\assemble-video.ps1
```

## Credentials

- Priorité : variables `DOC_EMAIL` / `DOC_PASSWORD`
- Sinon : fichier `.demo-credentials.json` (auto-créé au premier run via inscription API)
- Pour une démo riche (factures, écritures, TEJ) : voir [demo-data-checklist.md](expert-comptable/demo-data-checklist.md)

## Livrables

| Fichier | Description |
|---------|-------------|
| `expert-comptable/factutrust-partenariat-expert-comptable.mp4` | Vidéo finale (~4 min) |
| `expert-comptable/script-voix-off.md` | Script narration |
| `expert-comptable/raw/chapitre-*.webm` | Screencasts bruts |
| `expert-comptable/audio/chapitre-*.mp3` | Pistes TTS |
