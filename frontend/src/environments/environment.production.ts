export const environment = {
  production: true,
  // Same-origin, relative URL — nginx.conf proxies /api/ to the
  // "daftech-crm-api-new" backend service (see render.yaml) over Docker's
  // internal network. Do NOT hardcode an absolute onrender.com URL here:
  // that bypasses the nginx proxy entirely and can silently point the
  // frontend at a different/stale backend than the one Render actually
  // deploys, which is what caused the ticket-workflow "not found" errors.
  apiBaseUrl: '/api',
};