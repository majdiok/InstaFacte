# Thème Pluto

Le thème Pluto est importé dans le projet FactuTrust pour le layout (sidebar, header, topbar).

## Import du thème

Pour mettre à jour les fichiers du thème depuis le dossier Downloads :

```bash
npm run theme:import
```

Le script copie les assets (CSS, fonts, images) depuis `%USERPROFILE%\Downloads\pluto-1.0.0\pluto-1.0.0` vers ce dossier.

Pour spécifier une source différente :

```bash
PLUTO_THEME_SOURCE=C:\chemin\vers\pluto npm run theme:import
```

## Structure

- `css/` - Feuilles de style Bootstrap et custom
- `fonts/` - Font Awesome, Flaticon
- `images/` - Logos, images de layout
