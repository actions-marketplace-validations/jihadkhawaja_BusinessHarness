# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props NuGet.Config BusinessHarness.slnx ./
COPY locks/ locks/
COPY external/MAGICore/ external/MAGICore/
COPY src/ src/
COPY README.md LICENSE ./
RUN dotnet restore src/BusinessHarness.Cli/BusinessHarness.Cli.csproj --locked-mode --configfile NuGet.Config
RUN dotnet publish src/BusinessHarness.Cli/BusinessHarness.Cli.csproj -c Release --no-restore -o /app --self-contained false

FROM mcr.microsoft.com/dotnet/runtime:10.0.14 AS runtime
LABEL org.opencontainers.image.authors="Jihad Khawaja <jihadkhawaja@outlook.com>" \
      org.opencontainers.image.licenses="LicenseRef-BusinessHarness"
USER root
RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/ ./
COPY LICENSE ./
COPY external/MAGICore/LICENSE ./licenses/MAGICore/LICENSE
USER $APP_UID
ENTRYPOINT ["dotnet", "/app/BusinessHarness.Cli.dll"]
CMD ["--help"]
