// API_PROXY_TARGET lets docker-compose point this at the `api` service (Docker's internal DNS
// name) instead of localhost, since ng serve's dev-server proxy runs inside the frontend
// container and "localhost" there means the container itself, not the api container.
const target = process.env.API_PROXY_TARGET ?? 'http://localhost:8080';

export default {
  '/api': {
    target,
    secure: false,
  },
};
