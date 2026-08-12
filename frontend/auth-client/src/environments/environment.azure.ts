export const environment = {
  production: true,
  // Azure Static Web Apps serves only the static frontend - the API lives on its own
  // origin, so this must be the API's full URL (not a relative path like Docker's nginx
  // setup uses). Requires the API's Cors:AllowedOrigins to include this SWA's origin.
  apiUrl: 'https://REPLACE_WITH_API_URL/api',
};
