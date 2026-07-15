# Tests E2E - FactuTrust

## 🎯 Vue d'Ensemble

Tests end-to-end pour valider la fonctionnalité d'inscription et les corrections apportées.

## 📦 Installation

```bash
# Depuis la racine du projet frontend
npm install

# Installer Playwright (recommandé)
npm run install:playwright

# OU installer Selenium
npm install -D selenium-webdriver chromedriver
```

## 🚀 Exécution Rapide

### Prérequis

1. Backend démarré sur `https://localhost:7001`
2. Frontend démarré sur `http://localhost:4200`

### Commandes

```bash
# Tests Playwright (recommandé)
npm run test:e2e              # Mode headless
npm run test:e2e:ui           # Interface graphique
npm run test:e2e:headed       # Navigateur visible

# Tests Selenium
npm run test:e2e:selenium
```

## 📁 Structure

- `selenium.config.ts` : Configuration partagée
- `playwright.registration.spec.ts` : Tests Playwright
- `registration.selenium.spec.ts` : Tests Selenium WebDriver
- `screenshots/` : Screenshots des échecs

## 📖 Documentation

- Guide complet : `docs/SELENIUM_TESTING_GUIDE.md`
- Guide rapide : `docs/QUICK_START_SELENIUM.md`
