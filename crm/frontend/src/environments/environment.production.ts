/**
 * Production API base URL — absolute, pointing at the real deployed Daftech API.
 *
 * Why absolute and not the previous relative '/api':
 * The relative base only works when the frontend is served by this repo's
 * nginx image (frontend/nginx.conf reverse-proxies /api/ to the backend).
 * In production the frontend is served as a Render Static Site (a plain upload
 * of dist/daftech-crm/browser), which never reads nginx.conf and has no /api
 * proxy at all — so every /api/... call hit the static host and came back as
 * index.html / 404, the login request never reached the backend, and the UI
 * showed "Could not reach the server. Please check your connection and try
 * again." An absolute URL reaches the backend in BOTH deployment shapes
 * (the nginx proxy simply goes unused).
 *
 * Because this is now cross-origin, the API must allow the frontend origin:
 * set Cors__AllowedOrigins__0 (and __1, ...) on the API's Render service to
 * the real frontend origin(s) — see render.yaml and Program.cs.
 *
 * If the backend service is ever recreated and its onrender.com hostname
 * changes, update DEPLOYED_API_BASE_URL here AND the upstream in
 * frontend/nginx.conf.
 *
 * window.__DAFTECH_API_BASE_URL__ (optionally set by a <script> in index.html)
 * overrides this without a rebuild — handy for staging or a custom domain.
 */
const DEPLOYED_API_BASE_URL = 'https://daftech-crm-api-n7i2.onrender.com/api';

function resolveApiBaseUrl(): string {
  const override =
    typeof window !== 'undefined'
      ? (window as unknown as { __DAFTECH_API_BASE_URL__?: string }).__DAFTECH_API_BASE_URL__
      : undefined;

  return (override ?? DEPLOYED_API_BASE_URL).replace(/\/+$/, '');
}

export const environment = {
  production: true,
  apiBaseUrl: resolveApiBaseUrl(),
};
