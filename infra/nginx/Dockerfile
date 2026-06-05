FROM alpine:3.22

RUN apk add --no-cache nginx nginx-mod-http-headers-more \
    && mkdir -p /run/nginx /usr/share/nginx/html /var/log/nginx /var/lib/nginx/tmp \
    && chown -R nginx:nginx /run/nginx /usr/share/nginx/html /var/log/nginx /var/lib/nginx

EXPOSE 8080

USER nginx

CMD ["nginx", "-g", "daemon off;"]
