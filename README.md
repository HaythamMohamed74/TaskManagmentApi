# Task Management Backend API

A simple Task Management backend built with ASP.NET Core (.NET 10), following a clean,
DDD-style layered architecture. Users can register, log in, and manage their own tasks;
a seeded admin account can manage users. Task reads are cached in Redis, and newly
created tasks are handed off to a background worker for simulated processing.

## Architecture

The solution is split into four layers plus a test project, each with a single
responsibility and depending only inward (API → Infrastructure → Application → Domain):

```
TaskManagement.slnx
src/
  TaskManagement.Domain          Entities, enums, domain exceptions, repository interfaces.
                                  No dependencies on anything else — pure business model.
  TaskManagement.Application     DTOs, service interfaces/implementations (business logic),
                                  and abstractions for things Infrastructure provides
                                  (ICacheService, IJwtTokenGenerator, IPasswordHasher,
                                  ITaskProcessingQueue, ICurrentUserService).
  TaskManagement.Infrastructure  EF Core (DbContext, configurations, migrations, repositories),
                                  Redis cache implementation, JWT generation, password hashing,
                                  the in-memory background queue + BackgroundService, and
                                  admin DB seeding.
  TaskManagement.Api             Controllers, Program.cs composition root, Swagger/JWT wiring,
                                  global exception handling middleware.
tests/
  TaskManagement.Tests           xUnit + Moq tests for the Application-layer services
                                  (business rules: duplicate prevention, sorting, ownership).
```

Domain entities (`User`, `TaskItem`) encapsulate their own invariants (private setters,
validation in constructors) rather than being anemic data bags, and expose behavior methods
(`UpdateStatus`, `MarkDeleted`) instead of public setters — the core DDD idea this task asks
for, kept intentionally small in scope (no full aggregate root / domain event machinery,
since that would be overkill for this size of project).

## Tech stack

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core + SQL Server (any local SQL Server instance — LocalDB or
  SQL Server Express — for local dev, a SQL Server container in Docker)
- StackExchange.Redis
- JWT Bearer authentication (`System.IdentityModel.Tokens.Jwt`)
- Swashbuckle (Swagger/OpenAPI), with a JWT "Authorize" button
- Serilog (console logging)
- xUnit + Moq

## Running the project

### Option A — Docker Compose (recommended, no local installs needed)

Requires Docker Desktop.

```bash
docker compose up --build
```

This starts SQL Server, Redis, and the API together. The API applies EF Core migrations
and seeds the admin user automatically on startup. Once healthy, browse to:

- Swagger UI: http://localhost:8080/swagger

### Option B — Run locally against a SQL Server instance you already have

Requires the .NET 10 SDK and any local SQL Server instance — LocalDB or SQL Server
Express both work (this repo's `appsettings.json` defaults to LocalDB,
`Server=(localdb)\mssqllocaldb;...`; adjust `ConnectionStrings:DefaultConnection` if
you're using a named SQL Server Express instance instead, e.g.
`Server=YOUR-MACHINE\SQLEXPRESS;...`). You'll also need a Redis instance
reachable at `localhost:6379` — the easiest way is `docker run -p 6379:6379 redis:7-alpine`
if you have Docker, or point `ConnectionStrings:Redis` in `appsettings.json` at any Redis
you have.

```bash
dotnet ef database update --project src/TaskManagement.Infrastructure --startup-project src/TaskManagement.Api
dotnet run --project src/TaskManagement.Api
```

(The app also applies migrations automatically on startup, so the explicit `ef database
update` above is optional — it's just handy if you want the schema created up front.)

Swagger UI: http://localhost:5080/swagger (or whichever port `dotnet run` prints).

### Running the tests

```bash
dotnet test
```

## Seeded admin credentials

On first startup, a default admin user is seeded (see `AdminSeed` in `appsettings.json`):

- **Email:** `admin@example.com`
- **Password:** `Admin@123`

Change these in `appsettings.json` (or via environment variables /
`AdminSeed__Email` / `AdminSeed__Password`) before deploying anywhere real —
they're intentionally simple defaults for review purposes.

## Authentication & authorization

- `POST /api/auth/register` — anyone can register (always as role `User`).
- `POST /api/auth/login` — returns a JWT bearer token.
- `GET /api/auth/me` — returns the current user's profile (any authenticated user).
- `GET/POST/DELETE /api/admin/users/*` — **Admin role only** (`[Authorize(Roles = "Admin")]`).
- `POST/GET/PUT /api/tasks/*` — any authenticated user; a user may only read/update
  **their own** tasks. Attempting to access another user's task returns `404 Not Found`
  (not `403`) so a user can't tell the difference between "doesn't exist" and "exists but
  isn't yours" — a deliberate choice to avoid leaking other users' task IDs.

Click **Authorize** in Swagger UI and paste `Bearer <token>` (or just the raw token,
Swashbuckle's HTTP-bearer scheme prepends `Bearer ` for you) to call protected endpoints
from the docs page.

## Redis caching

`GET /api/tasks/{id}` is cached in Redis under key `task:{id}` with a 5-minute TTL:

1. First request → cache miss → loaded from SQL Server → written to Redis.
2. Subsequent requests → served straight from Redis.
3. `PUT /api/tasks/{id}/status` refreshes (overwrites) that same cache entry with the
   updated value, so readers never see stale data after a status change.

The background worker (below) also refreshes the cache entry after it moves a task from
`Pending` to `InProgress`, for the same reason.

**Resilience note:** cache reads/writes are wrapped so a Redis outage degrades to
"always hit the database" (logged as a warning) instead of failing the request — a cache
should never be a single point of failure for the primary read/write path.

## Background processing

`POST /api/tasks` saves the task to the database, then hands its ID to an in-memory queue
(`System.Threading.Channels.Channel<Guid>`). A hosted `BackgroundService`
(`TaskProcessingBackgroundService`) drains that queue, simulates processing work with a
short delay, and moves the task from `Pending` → `InProgress`, refreshing the Redis cache
entry to match. This satisfies the "simple background processing" requirement without an
external broker (RabbitMQ/etc.), per the task instructions.

## Business logic implemented

- **Sorting:** `GET /api/tasks` returns the current user's tasks sorted by priority
  (`High` → `Medium` → `Low`), then by creation date ascending (oldest first) within the
  same priority.
- **Duplicate prevention:** creating a task with a title that already exists for the same
  user, created on the same UTC calendar day, is rejected with `409 Conflict`.

## Other things implemented (bonus)

- **Global exception handling** — a single middleware maps domain exceptions
  (`NotFoundException`, `ConflictException`, `UnauthorizedException`, etc.) to the right
  HTTP status + a `application/problem+json` body; anything unexpected becomes a logged
  `500` without leaking internals.
- **Docker support** — `Dockerfile` + `docker-compose.yml` (API + SQL Server + Redis).
- **Unit tests** — `TaskService` and `AuthService` business rules (duplicate prevention,
  sorting, ownership checks, credential validation).
- **Structured logging** — Serilog to console.
- **Soft delete for users** — `DELETE /api/admin/users/{id}` sets `IsDeleted = true`
  rather than removing the row; deleted users are excluded from `GetAll`, login, and
  existence checks via an EF Core global query filter.

Not implemented (out of scope for the time box): refresh tokens — access tokens simply
expire after 60 minutes (configurable via `Jwt:ExpiryMinutes`) and the user logs in again.

## Assumptions

- **Database:** SQL Server was chosen over PostgreSQL (both were offered as options).
  Any local SQL Server instance (LocalDB or SQL Server Express) works for zero-friction
  local development on Windows; Docker Compose ships a real SQL Server 2022 container
  for anything closer to production.
- **"Sort by priority"** is interpreted as highest-priority-first (`High`, `Medium`, `Low`),
  since the task description didn't specify a direction.
- **"Duplicate task" scope** is per-user, per-title, per UTC calendar day (matching "same
  title on the same day for the same user" from the spec) — titles are compared after
  trimming whitespace, case-sensitively.
- **Ownership violations return 404, not 403** (see Authorization section above) — a
  deliberate anti-enumeration choice, called out since the spec didn't say either way.
- **Background processing target status:** the spec doesn't say what the "processed"
  status should be, only that the worker should "update the task accordingly." It moves
  `Pending` → `InProgress` (representing "picked up and being worked on"), leaving the
  user free to mark it `Done` themselves via the status endpoint.
- **JWT secret** in `appsettings.json` is a placeholder for review purposes — replace
  `Jwt:Secret` with a real secret (e.g. via environment variable or user-secrets) for any
  non-local use.
