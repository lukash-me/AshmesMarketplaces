# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
WORKDIR /src/Frontend

COPY Frontend/package.json Frontend/package-lock.json ./
RUN npm ci

COPY Frontend/ ./
RUN npm run build

FROM nginx:1.27-alpine AS final

COPY Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/Frontend/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
