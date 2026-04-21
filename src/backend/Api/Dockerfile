FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/backend/ERP.sln src/backend/ERP.sln
COPY src/backend/Api/ERP.Api.csproj src/backend/Api/
COPY src/backend/Application/ERP.Application.csproj src/backend/Application/
COPY src/backend/Domain/ERP.Domain.csproj src/backend/Domain/
COPY src/backend/Infrastructure/ERP.Infrastructure.csproj src/backend/Infrastructure/

RUN dotnet restore src/backend/Api/ERP.Api.csproj

COPY src/backend/ src/backend/

WORKDIR /src/src/backend/Api
RUN dotnet publish ERP.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet ERP.Api.dll"]
