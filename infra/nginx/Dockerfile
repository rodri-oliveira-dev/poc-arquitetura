FROM alpine:3.22

RUN apk add --no-cache nginx nginx-mod-http-headers-more \
    && mkdir -p /run/nginx /usr/share/nginx/html /var/log/nginx

EXPOSE 443

CMD ["nginx", "-g", "daemon off;"]
