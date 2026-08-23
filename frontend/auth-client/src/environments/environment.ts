export const environment = {
  production: true,
  // In Docker, nginx reverse-proxies /api to the api container, so the frontend and
  // backend are same-origin - no CORS or cross-site cookie complications.
  apiUrl: '/api'
};
