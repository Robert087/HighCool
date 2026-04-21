FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared solution/config files first
COPY ERP.sln ./
COPY Directory.Build.props* ./
COPY Directory.Packages.props* ./
COPY NuGet.config* ./

# Copy project files first for restore caching
COPY Api/ERP.Api.csproj Api/
COPY Application/ERP.Application.csproj Application/
COPY Domain/ERP.Domain.csproj Domain/
COPY Infrastructure/ERP.Infrastructure.csproj Infrastructure/

RUN dotnet restore Api/ERP.Api.csproj

# Copy the rest of the source
COPY . .

RUN dotnet publish Api/ERP.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "ERP.Api.dll"]