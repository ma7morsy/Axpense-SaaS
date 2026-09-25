# Run Axpense locally

## 1. Install (once)

| Tool | Version | Check |
|---|---|---|
| .NET SDK | **10.0** | `dotnet --version` |
| Node.js | 20 or newer | `node -v` |
| Docker Desktop | any recent | `docker -v` |

Trust the ASP.NET development HTTPS certificate once: `dotnet dev-certs https --trust`

## 2. Start the database

From the project folder (where `docker-compose.yml` is):

```bash
docker compose up -d db
```

This runs PostgreSQL 17 on `localhost:5432` (database, user and password are all `axpense`).

If you ran an older version of the project before, recreate the database once, because the tables changed:

```bash
docker compose down -v
docker compose up -d db
```

## 3. Start the API

```bash
cd src/Axpense.Api
dotnet run --launch-profile Axpense.Api
```

- The API runs on **https://localhost:63521**, with Swagger at `/swagger`.
- On first start it creates the tables and the demo account.
- Keep this terminal open.

In Visual Studio you can instead open `Axpense.sln`, set **Axpense.Api** as the startup project and press F5.

## 4. Start the web app

In a second terminal:

```bash
cd src/Axpense.Web
npm install
npm run dev
```

Open **http://localhost:5173**. The web app forwards `/api` to `https://localhost:63521` (see `vite.config.ts`).

## 5. Sign in

- Demo account: **admin@axpense.local** / **Axpense123!** (the login page has a "Use demo" button in dev mode).
- Or choose **Create an account** to register a new company; it goes through the onboarding wizard.

## Troubleshooting

| Problem | Fix |
|---|---|
| `Failed to connect to 127.0.0.1:5432` | The database isn't running. Run `docker compose up -d db` and wait a few seconds. |
| Web app shows network errors / 502 | The API isn't running, or runs on another port. Start step 3 and check the port in `vite.config.ts`. |
| `Failed to resolve import ...` in Vite | Run `npm install` again, then `npm run dev -- --force`. |
| Errors about missing columns/tables | Recreate the database (step 2, `down -v`). |
| HTTPS certificate warnings | `dotnet dev-certs https --clean` then `dotnet dev-certs https --trust`. |

## Notes

- **Password reset:** email isn't configured yet. In Development the "Check your email" screen shows the reset link directly.
- **Uploaded files** (logos, photos, documents) are stored under `src/Axpense.Api/bin/.../storage` unless `FileStorage:RootPath` is set in `appsettings.json`.
- **Production:** see `docker-compose.prod.yml`, `.env.example` and `docs/DEPLOYMENT.md`.
- **Module docs** are in `docs/`.
