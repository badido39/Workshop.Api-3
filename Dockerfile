# ---------- Étape 1 : runtime (base) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# ---------- Étape 2 : SDK et build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# On copie d'abord le .csproj : le restore reste en cache tant que les paquets ne changent pas
COPY ["Workshop.Api.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Étape 3 : image finale ----------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Workshop.Api.dll"]
