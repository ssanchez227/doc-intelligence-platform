# Build context: src/worker
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DocIntel.Worker/DocIntel.Worker.csproj DocIntel.Worker/
RUN dotnet restore DocIntel.Worker/DocIntel.Worker.csproj
COPY DocIntel.Worker/ DocIntel.Worker/
RUN dotnet publish DocIntel.Worker/DocIntel.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "DocIntel.Worker.dll"]
