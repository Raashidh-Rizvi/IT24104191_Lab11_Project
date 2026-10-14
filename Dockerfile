# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY CampusEats.Api/CampusEats.Api.csproj ./CampusEats.Api/
RUN dotnet restore CampusEats.Api/CampusEats.Api.csproj
COPY CampusEats.Api/ ./CampusEats.Api/
RUN dotnet publish CampusEats.Api/CampusEats.Api.csproj -c Release -o /app --no-restore

# ---- Run stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CampusEats.Api.dll"]
