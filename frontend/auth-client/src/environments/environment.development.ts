export const environment = {
  production: false,
  // ng serve's dev-server proxy (proxy.conf.json) forwards /api to the backend on :8080, so the
  // frontend and backend are same-origin from the browser's point of view - no CORS or
  // cross-site cookie complications, matching the Docker/SWA setups.
  apiUrl: '/api',
  // Left empty so local dev never sends traffic to GA - AnalyticsService no-ops without an ID.
  gaMeasurementId: '',
};
