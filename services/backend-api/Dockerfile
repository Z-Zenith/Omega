FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BackendApi.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
# #131: Data Protection key ring (used to encrypt TOTP secrets) is persisted here via a
# named volume (see docker-compose.yml) so keys survive container restarts/redeploys.
# Must be owned by the non-root $APP_UID the app runs as, or PersistKeysToFileSystem
# fails to write on first startup.
RUN mkdir -p /keys && chown $APP_UID:$APP_UID /keys
USER $APP_UID
ENTRYPOINT ["dotnet", "BackendApi.dll"]
