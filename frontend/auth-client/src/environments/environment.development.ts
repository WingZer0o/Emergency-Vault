export const environment = {
  production: false,
  // Local `ng serve` runs on :4200, cross-origin from the API on :8080 - the backend's
  // CORS policy (Cors:AllowedOrigins) must include http://localhost:4200.
  apiUrl: 'http://localhost:8080/api',
  // Left empty so local dev never sends traffic to GA - AnalyticsService no-ops without an ID.
  gaMeasurementId: '',
};
