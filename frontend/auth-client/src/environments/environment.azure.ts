export const environment = {
  production: true,
  // Azure Static Web Apps serves only the static frontend - the API lives on its own
  // origin, so this must be the API's full URL (not a relative path like Docker's nginx
  // setup uses). Requires the API's Cors:AllowedOrigins to include this SWA's origin.
  apiUrl: 'https://ev-backend.lemonflower-3f9fde98.westus2.azurecontainerapps.io/api',
  // GA4 measurement ID - replace with the real one before deploying. AnalyticsService treats
  // an empty string as "analytics disabled".
  gaMeasurementId: 'G-XXXXXXXXXX',
};
