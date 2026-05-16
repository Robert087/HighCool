# HighCool ERP Foundation

Initial scaffold for a modular monolith ERP web application with:

* ASP.NET Core Web API backend
* React + TypeScript frontend
* Clean layered structure for future ERP modules

## Project Structure

```text
src/
  backend/
    Api/
    Application/
    Domain/
    Infrastructure/
    tests/
  frontend/
docs/
```

## Backend

### Prerequisites

* .NET SDK 8.0+

### Run

```bash
cd src/backend/Api
dotnet restore
dotnet run
```

In development, the API uses a local SQLite file (`highcool-dev.db`) by default so the master-data screens can run without a local SQL Server instance. The Railway container defaults to SQLite at `/app/data/highcool.db`; attach a Railway volume at `/app/data` for production persistence, or override the provider and connection string for SQL Server.

### Health Check

```bash
curl http://localhost:5080/health
```

Expected response:

```text
OK
```

## Frontend

### Prerequisites

* Node.js 20+
* npm 10+

### Run

```bash
cd src/frontend
npm install
npm run dev
```

The Vite dev server will print the local URL in the terminal.

### Build

```bash
cd src/frontend
npm run build
```

## Notes

* The backend includes EF Core configuration for SQLite and SQL Server.
* Posting and ledger logic must remain server-side.
* The frontend reads `VITE_API_BASE_URL` for deployed API calls.

## Deployment

Railway and Vercel deployment steps are documented in [docs/deployment.md](/home/botmother/HighCoolProduction/docs/deployment.md).

Current production targets:

* Backend: `https://highcool-production-production.up.railway.app`
* Frontend: `https://high-cool-production.vercel.app`

Required production variables:

```bash
# Railway
ASPNETCORE_ENVIRONMENT=Production
DatabaseProvider=Sqlite
ConnectionStrings__DefaultConnection="Data Source=/app/data/highcool.db"
Cors__AllowedOrigins__0=https://high-cool-production.vercel.app

# Vercel
VITE_API_BASE_URL=https://highcool-production-production.up.railway.app
```

## Database Baseline

### Prerequisites

* SQLite or SQL Server reachable from the backend connection string
* `dotnet tool restore`

### Default Connection

The default backend connection string is defined in [appsettings.json](/home/botmother/HighCoolProduction/src/backend/Api/appsettings.json).

For local development, [appsettings.Development.json](/home/botmother/HighCoolProduction/src/backend/Api/appsettings.Development.json) switches the provider to SQLite and auto-creates a seeded `highcool-dev.db` file when the API starts.

You can override it from the terminal:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HighCoolERP;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
export DatabaseProvider="SqlServer"
```

### Create Or Apply Migrations

```bash
dotnet tool restore
dotnet ef migrations list --project src/backend/Infrastructure/ERP.Infrastructure.csproj --startup-project src/backend/Api/ERP.Api.csproj
dotnet ef database update --project src/backend/Infrastructure/ERP.Infrastructure.csproj --startup-project src/backend/Api/ERP.Api.csproj
```
