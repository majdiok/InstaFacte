const target = process.env.E2E_API_BASE ?? 'https://localhost:7001';

module.exports = {
  '/api': {
    target,
    secure: false,
    changeOrigin: true
  },
  '/health': {
    target,
    secure: false,
    changeOrigin: true
  },
  '/assets/powerpoint': {
    target,
    secure: false,
    changeOrigin: true
  }
};
