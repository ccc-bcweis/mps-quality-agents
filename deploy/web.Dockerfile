# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
# Placeholder console. The UI lane replaces this. There is no web/package.json yet.

FROM nginx:1.27-alpine
COPY deploy/web/index.html /usr/share/nginx/html/index.html
EXPOSE 80
