export const environment = {
  production: false,
  // URL relative en dev : Angular dev-server proxifie /api vers https://localhost:7001
  // (voir proxy.conf.json). Évite cert HTTPS auto-signé et tout pré-flight CORS.
  apiUrl: '/api'
};
