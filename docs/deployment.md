# Deployment - Railway Backend and Vercel Frontend

This project is ready to deploy as:

* ASP.NET Core backend on Railway
* React/Vite frontend on Vercel
* SQLite on Railway with a persistent volume, or SQL Server via connection string

## URLs

Current production targets:

* Backend: `https://highcool-production-production.up.railway.app`
* Frontend: `https://high-cool-production.vercel.app`

If the old Railway or Vercel projects are removed and recreated, replace these URLs in the
environment variables below.

## Railway Backend

Deploy the repository root to Railway.

Railway settings:

* Root directory: repository root
* Builder: Dockerfile
* Dockerfile path: `Dockerfile`
* Health check path: `/health`
* Public port: `8080`

Required variables:

```bash
ASPNETCORE_ENVIRONMENT=Production
DatabaseProvider=Sqlite
ConnectionStrings__DefaultConnection=Data Source=/app/data/highcool.db
DataProtection__KeysPath=/app/data/dataprotection-keys
Cors__AllowedOrigins__0=https://high-cool-production.vercel.app
```

For SQLite production persistence, attach a Railway volume mounted at:

```text
/app/data
```

Without this volume, the SQLite database can be lost when Railway rebuilds or replaces the
container.

The Docker image declares `/app/data` as a volume, and the local `docker-compose.yml` uses a
named volume mounted to the same path. Railway still requires creating or attaching the
volume on the Railway service itself; keep the mount path exactly `/app/data`.

The backend container also sets these safe defaults:

```bash
DatabaseProvider=Sqlite
ConnectionStrings__DefaultConnection=Data Source=/app/data/highcool.db
DataProtection__KeysPath=/app/data/dataprotection-keys
ASPNETCORE_URLS=http://+:8080
```

When `DatabaseProvider=Sqlite`, the API applies EF Core migrations on startup. Development
also seeds sample master data.

### SQL Server Option

To use SQL Server instead of SQLite, set:

```bash
DatabaseProvider=SqlServer
ConnectionStrings__DefaultConnection=Server=HOST,1433;Database=HighCoolERP;User Id=USER;Password=PASSWORD;TrustServerCertificate=True
```

Then apply migrations from a machine with the .NET SDK:

```bash
cd src/backend
dotnet tool restore
dotnet ef database update --project Infrastructure/ERP.Infrastructure.csproj --startup-project Api/ERP.Api.csproj
```

## Vercel Frontend

Deploy `src/frontend` as the Vercel project root.

Vercel settings:

* Root directory: `src/frontend`
* Install command: `npm install`
* Build command: `npm run build`
* Output directory: `dist`

Required variable:

```bash
VITE_API_BASE_URL=https://highcool-production-production.up.railway.app
```

The frontend includes `vercel.json` to rewrite browser routes to `index.html` for the
React single-page app.

## Replacement Order

When replacing an old deployment with this project:

1. Copy Railway variables, Vercel variables, custom domains, and database or volume settings
   from the old projects.
2. Deploy the Railway backend and wait for `/health` to return `OK`.
3. Set the Vercel `VITE_API_BASE_URL` variable to the Railway backend URL.
4. Deploy the Vercel frontend.
5. Set `Cors__AllowedOrigins__0` on Railway to the final Vercel frontend URL.
6. Redeploy Railway if CORS variables changed.
7. Verify the frontend can call the backend without CORS errors.
8. Remove the old Railway/Vercel projects only after the new backend, frontend, and database
   persistence are confirmed.

Do not delete the old Railway database or volume until the old data is confirmed disposable or
backed up.

## Verification

Local backend:

```bash
cd src/backend
dotnet test
dotnet run --project Api/ERP.Api.csproj
curl http://localhost:5080/health
```

Local frontend:

```bash
cd src/frontend
npm install
npm run build
```

Production:

```bash
curl https://highcool-production-production.up.railway.app/health
curl https://highcool-production-production.up.railway.app/
```

Expected health response:

```text
OK
```
