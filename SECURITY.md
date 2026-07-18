# Sécurité des dépendances frontend

## Contexte

Les applications Angular (`factutrust-web`, `factutrust-backoffice`) sont auditées via `npm audit`.
Une migration vers **Angular 19.2 LTS** a été réalisée pour corriger les CVE framework (XSS, XSRF, HttpTransferCache, DoS formatage).

## Processus trimestriel

1. Sur chaque frontend :
   ```powershell
   cd src/Frontend/factutrust-web   # ou factutrust-backoffice
   npm audit
   npm outdated
   ```
2. Appliquer les correctifs non-breaking :
   ```powershell
   npm audit fix --legacy-peer-deps
   ```
3. **Ne jamais** exécuter `npm audit fix --force` sans revue : npm peut proposer un saut vers Angular 21+.
4. Valider avec `scripts/verify-all.ps1` et, pour le web, `npm run test:e2e` (smoke) + `npm run build:prod`.
5. Mettre à jour Angular uniquement via `npx ng update` version par version (N → N+1).

## Seuils CI

- Échec CI si une vulnérabilité **critical** est détectée (`npm audit --audit-level=critical`).
- Les advisories **high** restantes sont principalement dans la toolchain de build/dev (`vite`, `webpack-dev-server`, `tar` via Angular CLI) et n’exposent pas le bundle runtime navigateur.

## Risques résiduels acceptés

| Zone | Justification |
|------|---------------|
| Toolchain Angular CLI / Vite / webpack-dev-server | Exposition limitée à l’environnement de développement et CI ; correction complète liée aux prochaines versions majeures du CLI |
| Advisories Angular signalés sur `<=19.2.25` pour certains CVE récents | Correctifs XSRF/XSS principaux inclus dès 19.2.16+ ; montée 20+ planifiée séparément si de nouveaux CVE critiques apparaissent |

## Exports Excel

La dépendance `xlsx` (SheetJS Community, sans correctif) a été remplacée par **`exceljs`** pour les exports Studio et AI Assistant.
