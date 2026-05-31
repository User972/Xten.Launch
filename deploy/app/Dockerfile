# nopCommerce 4.90 on .NET 9 — built from source.
#
# NOTE: This is a vetted starting-point Dockerfile. The canonical, version-tracked
# Dockerfile is maintained in the official nopSolutions/nopCommerce_docker repo —
# cross-check the plugin-copy/publish steps there for your exact tag.
# Building the whole solution first ensures plugin projects compile and land in
# Presentation/Nop.Web/Plugins so they are included in the publish output.

ARG NOP_VERSION=release-4.90.4

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG NOP_VERSION
WORKDIR /src
RUN git clone --depth 1 --branch "${NOP_VERSION}" https://github.com/nopSolutions/nopCommerce.git .

# Include the custom theme so it is published as Nop.Web/Themes/EbookIndonesia.
# The build context is the REPOSITORY ROOT (see deploy/docker-compose.yml), so this path
# is repo-root-relative. nopCommerce globs Themes/** as content, so the theme's Views/CSS/JS
# are copied into the publish output exactly like the built-in DefaultClean theme.
COPY themes/EbookIndonesia/ src/Presentation/Nop.Web/Themes/EbookIndonesia/
# OPTIONAL — build the Midtrans payment plugin into the image the same way (uncomment):
# COPY plugins/Nop.Plugin.Payments.Midtrans/ src/Plugins/Nop.Plugin.Payments.Midtrans/

RUN dotnet restore src/NopCommerce.sln
RUN dotnet build   src/NopCommerce.sln -c Release
RUN dotnet publish src/Presentation/Nop.Web/Nop.Web.csproj -c Release -o /app/published --no-restore

# ---------- runtime ----------
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
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=120s --retries=5 \
  CMD curl -fsS http://localhost:8080/ >/dev/null || exit 1
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
CMD ["dotnet", "Nop.Web.dll"]
