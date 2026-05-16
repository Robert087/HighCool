FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Api/ERP.Api.csproj Api/
COPY Application/ERP.Application.csproj Application/
COPY Domain/ERP.Domain.csproj Domain/
COPY Infrastructure/ERP.Infrastructure.csproj Infrastructure/

RUN dotnet restore Api/ERP.Api.csproj

COPY . .

RUN dotnet publish Api/ERP.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/data

ENV ASPNETCORE_URLS=http://+:8080
ENV DatabaseProvider=Sqlite
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/highcool.db"
ENV DataProtection__KeysPath=/app/data/dataprotection-keys
VOLUME ["/app/data"]
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "ERP.Api.dll"]
