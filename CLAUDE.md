# net

Monorepo: `apps/web` (React + Vite + TypeScript), `apps/api` (ASP.NET Core 8 minimal API, C#, Postgres via Npgsql + Dapper), `apps/api.tests` (xUnit), `e2e/` (Playwright).

## Rules for automated tasks
- Work only inside this repo, on the branch you are given. Never push, never touch `main`, never edit `.github/workflows`, `apps/api/Dockerfile` or deploy config unless the task says so.
- Never read, print or create secrets. Do not add `.env` files with real values.
- Keep changes small and focused on the task. Add or update tests for every behaviour change (xUnit in `apps/api.tests`, e2e in `e2e/`).
- Before finishing run `npm run verify` (lint, dotnet test, build, e2e) and fix failures. Do not disable or delete tests to make them pass. The C# build treats warnings as errors.
- Web calls the API through `VITE_API_URL` (default `http://localhost:3001`). The API reads `DATABASE_URL` (optional locally: health reports `db: disabled`) and `CORS_ORIGINS`.
- Tests that persist data need Postgres: `npm run verify` runs with `DATABASE_URL` already pointing at a throwaway local postgres (docker compose service `db`, port 55432). Add tables as `CREATE TABLE IF NOT EXISTS` statements to the `Schema` array in `apps/api/Db.cs` (it runs once under an advisory lock), never rely on manual setup. Use `await using var conn = await db.OpenAsync();` with Dapper for queries.
- Keep dependencies light and ASP.NET Core 8 compatible (net8.0). Use UTC for dates.
- The first page of the web app is used as the portfolio screenshot, so it must look good and show realistic seeded data.

## Commands
- `npm install` — install the web workspace
- `npm run verify` — full local check, same as CI
- `npm run start:api` — run the built API on :3001 (build first with `npm run build`)
- `npm run dev --workspace web` — web on :3000
- `docker compose up -d db` then `DATABASE_URL=postgres://postgres:postgres@localhost:55432/app` for a local DB
