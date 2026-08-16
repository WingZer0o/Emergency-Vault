export const environment = {
  production: true,
  // In Docker, nginx reverse-proxies /api to the api container, so the frontend and
  // backend are same-origin - no CORS or cross-site cookie complications.
  apiUrl: '/api',
  // GA4 measurement ID - replace with the real one before deploying. AnalyticsService treats
  // an empty string as "analytics disabled".
  gaMeasurementId: 'G-81QJT6YXRE',
};
