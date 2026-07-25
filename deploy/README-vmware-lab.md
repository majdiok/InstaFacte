
# Test Docker FactuTrust — VM Ubuntu VMware (16 Go)

Guide lab complémentaire à [`README.md`](README.md). Aucune modification du code applicatif.

## Prérequis VM

- Ubuntu 22.04+, **16 Go RAM**, 4 vCPU, 80 Go disque
- Réseau : **Bridged** (LAN `192.168.1.x`) ou **NAT** VMware (`192.168.179.x` via VMnet8) si Bridged échoue
- Accès SSH depuis Windows

**Config lab actuelle (exemple NAT) :** `deploy/vm.lab.local.json` → `VmHost: 192.168.179.100`, `VmUser: sabiko`

## 0. Dépannage réseau VMware (169.254.x.x)

Si `ip -4 addr show` affiche **`169.254.x.x`** sur `ens33`, la VM n'a pas reçu d'adresse DHCP.

**Cause fréquente :** adaptateur VMware en VMnet custom/NAT cassé, ou services VMware NAT/DHCP arrêtés sur Windows.

### Windows — forcer Bridged

```powershell
.\deploy\scripts\set-vm-bridged.ps1 -Restart
.\deploy\scripts\restart-vmware-network.ps1   # PowerShell admin
```

VMware Workstation : VM **éteinte** → Settings → Network Adapter → **Bridged** (Wi‑Fi/Ethernet `192.168.1.x`) → redémarrer.

### VM — IP statique Bridged (sans apt, si DHCP échoue)

**Avant tout `apt update`** tant que l'IP est APIPA :

```bash
BRIDGED_STATIC=1 IP=192.168.1.100 GATEWAY=192.168.1.1 bash deploy/scripts/fix-vm-network.sh
```

Ou manuellement :

```bash
sudo ip addr flush dev ens33
sudo ip addr add 192.168.1.100/24 dev ens33
sudo ip link set ens33 up
sudo ip route replace default via 192.168.1.1
echo -e "nameserver 8.8.8.8\nnameserver 1.1.1.1" | sudo tee /etc/resolv.conf
ping -c 2 192.168.1.17   # PC hôte
sudo apt update && sudo apt install -y openssh-server
sudo systemctl enable --now ssh
```

### Console VMware (sans SSH) — Windows

Si la VM est visible dans VMware Workstation :

```powershell
# Option A : variable d'environnement
$env:VM_GUEST_PASSWORD = 'mot-de-passe-ubuntu'
.\deploy\scripts\prepare-vm-network.ps1

# Option B : fichier local (gitignore)
# deploy/vm.guest-password.local — une ligne, mot de passe sabiko
.\deploy\scripts\prepare-vm-network.ps1
```

### Découverte IP + preflight

```powershell
.\deploy\scripts\discover-vm.ps1
.\deploy\scripts\preflight-vm.ps1 -Discover
```

Quand l'IP est correcte, mettre à jour `deploy/vm.lab.local.json` :

```json
{ "VmHost": "192.168.1.100", "VmUser": "sabiko" }
```

**Bootstrap sans SSH (partage VMware ou clé USB) :** copier le dépôt sur la VM puis :

```bash
cd /chemin/vers/FactuTrustCopy
bash deploy/scripts/vm-console-bootstrap.sh
```

## 1. Configurer la connexion Windows → VM

```powershell
copy deploy\vm.lab.local.json.example deploy\vm.lab.local.json
# Éditer VmHost avec l'IP de la VM
```

## 2. Orchestration depuis Windows (recommandé)

```powershell
# Setup VM (SSH, Docker, swap) + sync + deploy
.\deploy\scripts\lab-from-windows.ps1 -SetupVm -Deploy -UpdateHosts
```

Étapes manuelles équivalentes :

| Étape | Commande |
|-------|----------|
| Setup VM | `sudo bash deploy/scripts/setup-vmware-lab.sh` (sur la VM) |
| Sync code | `.\deploy\scripts\sync-to-vm.ps1 -VmHost IP -VmUser deploy` |
| Deploy | `bash deploy/scripts/deploy-lab.sh` (sur la VM) |

## 3. Accès

- Web : `http://factutrust.local/` (fichier hosts Windows)
- Backoffice : `http://factutrust.local/admin/`
- Identifiants : `deploy/.env` → `Bootstrap__PlatformAdmin__*`

## 4. Compose lab

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.lab.yml up -d
```

Overrides : SMTP off, Ollama 1 modèle, migrations tenant au boot, Nginx HTTP-only, SQL Server `1433` publié, cabinets comptables (`Features__AccountingFirms__Enabled=true`), auto-caisse sur paiements espèces (`Features__AutoCashFromInvoicePayment` / `AutoCashFromSupplierPayment`).

### SQL Server (SSMS / Azure Data Studio depuis Windows)

Le lab publie `1433:1433` sur `factutrust-sqlserver`. Connexion depuis l’hôte Windows :

| Champ | Valeur |
|-------|--------|
| Serveur | `192.168.179.100,1433` |
| Auth | SQL Server (`sa`) |
| Mot de passe | `MSSQL_SA_PASSWORD` dans `deploy/.env` sur la VM |

Lister les bases sur la VM :

```bash
PASS=$(grep '^MSSQL_SA_PASSWORD=' deploy/.env | cut -d= -f2-)
sudo docker exec -i factutrust-sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$PASS" -C \
  -Q "SELECT name FROM sys.databases ORDER BY name"
```

### Fallback Ollama hôte (Docker Hub lent)

L’image `ollama/ollama:latest` fait ~3 Go compressés. Si le pull Docker Hub
reste bloqué, installer Ollama sur l’hôte Ubuntu et utiliser l’override lab :

```bash
# Sur la VM — préférer un téléchargement depuis Windows puis scp si le réseau VM est lent
bash deploy/scripts/install-host-ollama.sh

docker compose \
  -f deploy/docker-compose.yml \
  -f deploy/docker-compose.lab.yml \
  -f deploy/docker-compose.lab-host-ollama.yml \
  up -d

bash deploy/scripts/pull-ollama-models-lab-host.sh
```

`deploy-lab.sh` active automatiquement cet override si `ollama/ollama:latest`
est absent (`USE_HOST_OLLAMA=0` pour forcer l’image Docker).

## 5. Tests

```bash
bash deploy/scripts/smoke-test.sh http://factutrust.local
```

Checklist fonctionnelle : inscription tenant, login, facture brouillon, upload logo, chat IA (3B).

## 6. Dépannage

Voir le plan VMware — RAM < 500 Mo → ne pas pull 7B/llava. CORS → aligner `AllowedOrigins` avec l'URL du navigateur.
