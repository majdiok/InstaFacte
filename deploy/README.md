# FactuTrust — déploiement Docker sur VPS Hostinger (Ubuntu)

Guide opérationnel pour déployer la stack complète : **SQL Server 2022**, **API .NET 8**, **Ollama**, **Nginx**, applications **Angular** (web `/`, backoffice `/admin`) sur un **domaine unique**.

## Architecture

| Composant | Rôle |
|-----------|------|
| `sqlserver` | Base master + bases tenant (création dynamique à l'inscription) |
| `api` | Backend FactuTrust.API, Hangfire, JWT, uploads |
| `ollama` | IA locale (modèles qwen2.5, llava) |
| `nginx` | TLS, reverse proxy, fichiers statiques Angular |
| `web-static-init` / `admin-static-init` | Copie des builds Angular vers volumes Nginx (one-shot) |

Volumes persistants : `sqlserver-data`, `api-wwwroot`, `api-appdata`, `api-logs`, `ollama-models`, certificat DataProtection.

## Prérequis VPS Hostinger

- Ubuntu **22.04 LTS** (ou 24.04)
- **32 Go RAM** recommandés (SQL + Ollama 7B)
- 160 Go+ SSD
- Nom de domaine pointant vers l'IP du VPS (enregistrement **A**)
- Ports ouverts : **22**, **80**, **443**

## 1. Préparation du serveur

```bash
ssh root@VOTRE_IP

apt update && apt upgrade -y
apt install -y ca-certificates curl git ufw

# Utilisateur deploy (recommandé)
adduser deploy
usermod -aG sudo deploy

ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable

# Docker (officiel)
curl -fsSL https://get.docker.com | sh
usermod -aG docker deploy
apt install -y docker-compose-plugin
```

## 2. Installation FactuTrust

```bash
sudo mkdir -p /opt/factutrust
sudo chown deploy:deploy /opt/factutrust
cd /opt/factutrust

git clone https://VOTRE_REPO/FactuTrustCopy.git src
cd src

# Secrets + certificats bootstrap
bash deploy/scripts/init-secrets.sh

# Éditer la configuration
nano deploy/.env
```

Variables **obligatoires** à personnaliser dans `deploy/.env` :

- `FACTUTRUST_DOMAIN`
- `AllowedOrigins` → `https://VOTRE_DOMAINE`
- `App__FrontendBaseUrl` → `https://VOTRE_DOMAINE`
- `Bootstrap__PlatformAdmin__Email`
- `Smtp__*` si emails activés

## 3. Premier démarrage

Depuis la **racine du dépôt** :

```bash
# Option A — TLS (certificats self-signed générés par init-secrets, ou Let's Encrypt)
docker compose -f deploy/docker-compose.yml up -d --build

# Option B — HTTP uniquement (tests locaux / bootstrap sans TLS)
# Renommer la config HTTP :
#   mv deploy/nginx/conf.d/factutrust.conf deploy/nginx/conf.d/factutrust-ssl.conf.disabled
#   cp deploy/nginx/conf.d/factutrust-http-only.conf.example deploy/nginx/conf.d/factutrust.conf
# Puis : HTTP_PORT=80 HTTPS_PORT=443 docker compose -f deploy/docker-compose.yml up -d --build
```

Attendre que l'API soit prête (migrations master + seed, ~1–3 min) :

```bash
docker compose -f deploy/docker-compose.yml logs -f api
```

Télécharger les modèles Ollama :

```bash
bash deploy/scripts/pull-ollama-models.sh
```

Smoke test :

```bash
bash deploy/scripts/smoke-test.sh https://VOTRE_DOMAINE
# ou http://localhost en mode HTTP-only
```

## 4. Let's Encrypt (production)

Après validation DNS :

```bash
apt install -y certbot
docker compose -f deploy/docker-compose.yml stop nginx

certbot certonly --standalone -d VOTRE_DOMAINE

cp /etc/letsencrypt/live/VOTRE_DOMAINE/fullchain.pem deploy/nginx/certs/
cp /etc/letsencrypt/live/VOTRE_DOMAINE/privkey.pem deploy/nginx/certs/

# Restaurer factutrust.conf SSL si désactivé
docker compose -f deploy/docker-compose.yml up -d nginx
```

Renouvellement : `certbot renew` + recopier les certs + `docker compose restart nginx`.

## 5. Accès applications

| URL | Application |
|-----|-------------|
| `https://VOTRE_DOMAINE/` | Application web (ERP client) |
| `https://VOTRE_DOMAINE/admin/` | Backoffice plateforme |
| `https://VOTRE_DOMAINE/api/` | API REST |
| `https://VOTRE_DOMAINE/health/ready` | Readiness (SQL master) |
| `https://VOTRE_DOMAINE/hangfire` | Jobs Hangfire (restreindre en prod) |

**Premier login backoffice** : identifiants `Bootstrap__PlatformAdmin__*` (définis dans `.env` à la création). **Changer le mot de passe immédiatement.**

## 6. Migrations tenant (post-release)

En production, `TenantMigrations__ApplyOnStartup=false`. Après chaque release avec migrations EF **tenant** :

1. Se connecter au backoffice (PlatformAdmin)
2. Appliquer les migrations :
   ```http
   POST /api/platform/migrations/tenants/apply-migrations
   ```
3. Vérifier le statut :
   ```http
   GET /api/platform/migrations/tenants/migrations-status
   ```

Documentation : [`docs/backend-tenant-migrations.md`](../docs/backend-tenant-migrations.md)

## 7. Mise à jour (release)

```bash
cd /opt/factutrust/src
bash deploy/scripts/release.sh
```

Puis migrations tenant via backoffice si nécessaire.

## 8. Sauvegardes

```bash
bash deploy/scripts/backup.sh
```

**Critique** : sauvegarder ensemble :
- Bases SQL (`FactuTrust_Master` + bases tenant)
- `deploy/certs/dataprotection.pfx` + mot de passe
- Volumes `api-wwwroot`, `api-appdata`

Sans le certificat DataProtection, les chaînes de connexion tenant chiffrées deviennent illisibles.

Planifier un cron quotidien :

```cron
0 2 * * * deploy bash /opt/factutrust/src/deploy/scripts/backup.sh
```

## 9. Dépannage

| Symptôme | Action |
|----------|--------|
| API ne démarre pas | `docker compose logs api` — vérifier JWT, SQL, certificat PFX |
| `502` sur `/api` | API pas healthy : `curl http://localhost:8080/health/ready` dans le conteneur |
| Nginx ne démarre pas | Certificats manquants dans `deploy/nginx/certs/` → utiliser config HTTP-only ou `init-secrets.sh` |
| Inscription tenant échoue | Compte SQL doit avoir `CREATE DATABASE` (utiliser `sa`) |
| `TENANT_MIGRATION_FAILED` | Appliquer migrations tenant via backoffice |
| OCR indisponible | Tessdata inclus dans l'image API ; vérifier logs `TesseractOcrService` |
| IA indisponible | `docker compose exec ollama ollama list` — relancer `pull-ollama-models.sh` |

## 10. WhatsApp (phase 2)

Désactivé par défaut (`Channels__Enabled=false`). Activation ultérieure :

1. Dépendances Chromium déjà préparées dans l'image API (Node + bridge)
2. Volume `api-whatsapp-session` persisté
3. `Channels__Enabled=true`, `WhatsAppEnabled=true`
4. Frontend : `channelsEnabled: true` dans `environment.prod.ts` + rebuild
5. Scanner QR via interface admin

## 11. Fichiers de référence

| Fichier | Description |
|---------|-------------|
| [`deploy/docker-compose.yml`](docker-compose.yml) | Stack complète |
| [`deploy/.env.example`](.env.example) | Variables d'environnement |
| [`deploy/Dockerfile.api`](Dockerfile.api) | Image API |
| [`deploy/nginx/conf.d/factutrust.conf`](nginx/conf.d/factutrust.conf) | Nginx SSL |
| [`deploy/scripts/`](scripts/) | init-secrets, release, backup, smoke |

## 12. Test lab VMware Ubuntu (16 Go)

Guide dédié : [`README-vmware-lab.md`](README-vmware-lab.md)

Orchestration depuis Windows :

```powershell
# 1. Éditer deploy\vm.lab.local.json (IP VM)
# 2. VM allumée + SSH actif
.\deploy\scripts\preflight-vm.ps1
.\deploy\scripts\lab-from-windows.ps1 -SetupVm -Deploy -UpdateHosts
```

## 13. Validation locale (développeurs)

Avant déploiement Hostinger :

```powershell
# Windows — porte qualité CI
powershell -File scripts\verify-all.ps1
```

```bash
# Linux / Docker — smoke stack
bash deploy/scripts/init-secrets.sh
docker compose -f deploy/docker-compose.yml up -d --build
bash deploy/scripts/smoke-test.sh https://localhost
```
