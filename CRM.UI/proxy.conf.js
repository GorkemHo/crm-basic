// Dev-server proxy: the browser talks to one origin; /api and /health go to the API.
// API_URL overrides the target (CI runs the API over plain HTTP).
const target = process.env.API_URL || 'https://localhost:7156';

module.exports = {
  '/api': { target, secure: false, changeOrigin: true },
  '/health': { target, secure: false, changeOrigin: true },
};
