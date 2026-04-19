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

* The backend includes EF Core and SQL Server base configuration only.
* No ERP business logic or data models are implemented yet.
* The frontend includes a minimal app shell, routing, and status UI only.

## Database Baseline

### Prerequisites

* SQL Server reachable from the backend connection string
* `dotnet tool restore`

### Default Connection

The default backend connection string is defined in [appsettings.json](/home/botmother/HighCool/src/backend/Api/appsettings.json).

You can override it from the terminal:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=HighCoolERP;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

### Create Or Apply Migrations

```bash
dotnet tool restore
dotnet ef migrations list --project src/backend/Infrastructure/ERP.Infrastructure.csproj --startup-project src/backend/Api/ERP.Api.csproj
dotnet ef database update --project src/backend/Infrastructure/ERP.Infrastructure.csproj --startup-project src/backend/Api/ERP.Api.csproj
```
