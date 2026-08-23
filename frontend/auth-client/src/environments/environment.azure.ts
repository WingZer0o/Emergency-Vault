export const environment = {
  production: true,
  // The Container App is linked to this Static Web App as a backend (az staticwebapp
  // backends link), so /api/* on the SWA's own origin proxies to it server-side - same
  // pattern as the Docker/nginx setup, just done at the Azure edge instead of nginx.
  apiUrl: '/api'
};
