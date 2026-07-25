# Place TLS certificates here before starting nginx with factutrust.conf (SSL mode):
#   fullchain.pem
#   privkey.pem
#
# Generate self-signed bootstrap certs:
#   ./deploy/scripts/init-secrets.sh
#
# Production (Let's Encrypt):
#   certbot certonly --webroot -w /var/www/certbot -d votre-domaine.tn
#   cp /etc/letsencrypt/live/votre-domaine.tn/fullchain.pem .
#   cp /etc/letsencrypt/live/votre-domaine.tn/privkey.pem .
