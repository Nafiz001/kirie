# syntax=docker/dockerfile:1
#
# The book as one process: the ASP.NET Core API, hosting the built SvelteKit
# app on the same origin, with the SQLite database on a volume.
#
#   docker build -t kirie .
#   docker run -p 8080:8080 -v kirie-data:/data -e Kirie__Salt=$(openssl rand -hex 16) kirie

# --- The book's pages ----------------------------------------------------------
FROM node:22-slim AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web .
RUN npm run build

# --- The API -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY server .
RUN dotnet publish Kirie.Api/Kirie.Api.csproj -c Release -o /out

# --- Runtime -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /out .
COPY --from=web /web/build ./web
USER root
RUN mkdir /data && chown $APP_UID /data
USER $APP_UID
# ASPNETCORE_FORWARDEDHEADERS_ENABLED trusts X-Forwarded-For, so the crane
# rate limit and the visitor fingerprint see the visitor rather than the
# reverse proxy. Only safe because the container is reached through a proxy
# that sets the header; do not publish its port directly to the internet.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Kirie__Web=/app/web \
    ConnectionStrings__Kirie="Data Source=/data/kirie.db"
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kirie.Api.dll"]
