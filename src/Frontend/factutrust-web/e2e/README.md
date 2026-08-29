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

## 🤖 CI GitHub Actions (smoke)

Le job **Web Playwright smoke** démarre SQL Server, build et lance l'API, puis enregistre un utilisateur de test avant les specs `sidebar-collapse` et `test-backend-connection`.

**Secret requis** dans les paramètres du dépôt GitHub (`Settings → Secrets and variables → Actions`) :

| Secret | Description |
|--------|-------------|
| `CI_SQL_SA_PASSWORD` | Mot de passe SA du conteneur SQL Server (respecter la politique de complexité Microsoft, ex. `Ci_Sql_Test_Pw1!Strong`) |

Credentials Playwright injectés automatiquement en CI :

- `FACTUTRUST_TEST_EMAIL` = `ci-e2e-smoke@factutrust.local`
- `FACTUTRUST_TEST_PASSWORD` = `Ci_E2e_Smoke_Pw1!Xy`

Scripts associés : `scripts/ci/wait-for-sql.sh`, `scripts/ci/start-api-e2e.sh`, `scripts/ci/e2e-register-user.sh`.

## 📖 Documentation

- Guide complet : `docs/SELENIUM_TESTING_GUIDE.md`
- Guide rapide : `docs/QUICK_START_SELENIUM.md`
