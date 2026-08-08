FROM node:24-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

FROM nginx:alpine AS final
COPY --from=build /app/dist/auth-client/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80

# Dev stage: no COPY of source - docker-compose.override.yml bind-mounts the app folder
# over /app at runtime and installs deps into a named volume so `ng serve` hot-reloads.
FROM node:24-alpine AS dev
WORKDIR /app
EXPOSE 4200
CMD ["sh", "-c", "npm install && npx ng serve --host 0.0.0.0 --port 4200 --poll 2000"]
