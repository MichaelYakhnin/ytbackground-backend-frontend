# Stage 1: Build the Angular app
FROM node:22 AS build

WORKDIR /app

ADD package.json .

RUN npm install

ADD . .

RUN npm run build:prod

FROM nginx:alpine

COPY --from=build /app/dist/ytbackground-frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]