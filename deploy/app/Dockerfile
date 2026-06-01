# nopCommerce 4.90 on .NET 9 — built from source.
#
# NOTE: This is a vetted starting-point Dockerfile. The canonical, version-tracked
# Dockerfile is maintained in the official nopSolutions/nopCommerce_docker repo —
# cross-check the plugin-copy/publish steps there for your exact tag.
# Building the whole solution first ensures plugin projects compile and land in
# Presentation/Nop.Web/Plugins so they are included in the publish output.

ARG NOP_VERSION=release-4.90.4

# ---------- build ----------
# Pinned to .NET 9: nopCommerce 4.90.4 targets net9.0. Do NOT bump to 10.x (Dependabot tried) until
# nopCommerce itself moves to .NET 10 — the published app is framework-dependent on the 9.0 runtime.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG NOP_VERSION
WORKDIR /src
RUN git clone --depth 1 --branch "${NOP_VERSION}" https://github.com/nopSolutions/nopCommerce.git .

# Include the custom theme so it is published as Nop.Web/Themes/EbookIndonesia.
# The build context is the REPOSITORY ROOT (see deploy/docker-compose.yml), so this path
# is repo-root-relative. nopCommerce globs Themes/** as content, so the theme's Views/CSS/JS
# are copied into the publish output exactly like the built-in DefaultClean theme.
COPY themes/EbookIndonesia/ src/Presentation/Nop.Web/Themes/EbookIndonesia/

# Build the Midtrans payment plugin into the image. Its csproj uses $(SolutionDir)
# ProjectReferences, so the project must be ADDED TO THE SOLUTION — a bare COPY alone
# won't compile it (the plugin DLL lands in Nop.Web/Plugins/Payments.Midtrans and is then
# included in the publish output).
COPY plugins/Nop.Plugin.Payments.Midtrans/ src/Plugins/Nop.Plugin.Payments.Midtrans/
RUN dotnet sln src/NopCommerce.sln add src/Plugins/Nop.Plugin.Payments.Midtrans/Nop.Plugin.Payments.Midtrans.csproj

RUN dotnet restore src/NopCommerce.sln
RUN dotnet build   src/NopCommerce.sln -c Release
RUN dotnet publish src/Presentation/Nop.Web/Nop.Web.csproj -c Release -o /app/published --no-restore

# ---------- runtime ----------
# Pinned to .NET 9 to match the build SDK and the net9.0 app (see note above). An aspnet:10 runtime
# image ships only the .NET 10 shared framework, so a net9.0 app would fail to start here.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update \
 && apt-get install -y --no-install-recommends gettext-base curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/published ./
COPY deploy/app/entrypoint.sh /usr/local/bin/entrypoint.sh
COPY deploy/config/dataSettings.template.json /app/config/dataSettings.template.json
RUN chmod +x /usr/local/bin/entrypoint.sh
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

# --- Drop root: run as the aspnet image's built-in non-root user (UID $APP_UID = 1654) ---
# nopCommerce writes at runtime to App_Data (dataSettings/appsettings/data-protection keys,
# install state), Plugins, wwwroot/images, and wwwroot/bundles. Hand the whole /app tree to the
# app user so those writes (and on-disk bundling) succeed. Named volumes mounted on these paths
# inherit this ownership when first created.
# UPGRADE NOTE: volumes first created by an OLDER root-based image are root-owned, so a fresh
# non-root container cannot write them. Recreate those volumes, or chown their mountpoints to
# 1654:1654 once (e.g. `docker compose run --rm --user 0 nopcommerce chown -R 1654:1654 \
# /app/App_Data /app/Plugins /app/wwwroot/images`).
RUN mkdir -p /app/App_Data /app/Plugins /app/wwwroot/images /app/wwwroot/files \
 && chown -R $APP_UID:$APP_UID /app
USER $APP_UID

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=120s --retries=5 \
  CMD curl -fsS http://localhost:8080/ >/dev/null || exit 1
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
CMD ["dotnet", "Nop.Web.dll"]
