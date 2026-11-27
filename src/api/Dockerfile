# Build context: src/api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DocIntel.Api/DocIntel.Api.csproj DocIntel.Api/
RUN dotnet restore DocIntel.Api/DocIntel.Api.csproj
COPY DocIntel.Api/ DocIntel.Api/
RUN dotnet publish DocIntel.Api/DocIntel.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The .NET images listen on 8080 and run as the non-root "app" user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DocIntel.Api.dll"]
