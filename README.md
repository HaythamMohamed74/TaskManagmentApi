# Task Management Backend API

A Task Management backend built with ASP.NET Core (.NET 10), structured in a DDD-style
layered architecture. Users can register, log in, and manage their own tasks. A seeded
admin account can manage users. Task reads are cached in Redis, and new tasks get handed
off to a background worker for simulated processing.

## Architecture

Four layers plus a test project, each depending only inward (API -> Infrastructure ->
Application -> Domain):

```
TaskManagement.slnx
src/
  TaskManagement.Domain          Entities, enums, domain exceptions, repository interfaces.
                                  No dependencies on anything else.
  TaskManagement.Application     DTOs, service interfaces/implementations, and abstractions
                                  for things Infrastructure provides (ICacheService,
                                  IJwtTokenGenerator, IPasswordHasher, ITaskProcessingQueue,
                                  ICurrentUserService).
  TaskManagement.Infrastructure  EF Core (DbContext, configurations, migrations,
                                  repositories), Redis cache, JWT generation, password
                                  hashing, the in-memory background queue + BackgroundService,
                                  admin seeding.
  TaskManagement.Api             Controllers, Program.cs, Swagger/JWT wiring, exception
                                  handling middleware.
tests/
  TaskManagement.Tests           xUnit + Moq tests for the Application-layer services.
```

`User` and `TaskItem` own their invariants: private setters, validation in the
constructor, behavior methods (`UpdateStatus`, `MarkDeleted`) instead of public setters.
No aggregate roots or domain events - the project is small enough that it didn't need them.

## Tech stack

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core + SQL Server (LocalDB or SQL Server Express locally, a SQL Server
  container in Docker)
- StackExchange.Redis
- JWT Bearer auth (`System.IdentityModel.Tokens.Jwt`)
- Swashbuckle (Swagger/OpenAPI), with a JWT Authorize button
- Serilog
- xUnit + Moq

## Running the project

### Option A: Docker Compose

Requires Docker Desktop.

```bash
docker compose up --build
```

Starts SQL Server, Redis, and the API together. Migrations and admin seeding run
automatically on startup.

Swagger UI: http://localhost:8080/swagger

### Option B: run it locally

Requires the .NET 10 SDK and a local SQL Server instance - LocalDB or SQL Server Express
both work. `appsettings.json` currently points at LocalDB
(`Server=(localdb)\mssqllocaldb;...`); change `ConnectionStrings:DefaultConnection` if
you're on a named instance instead. You'll also need Redis reachable at `localhost:6379`
(`docker run -p 6379:6379 redis:7-alpine` if you have Docker, or point
`ConnectionStrings:Redis` at whatever Redis you have).

```bash
dotnet ef database update --project src/TaskManagement.Infrastructure --startup-project src/TaskManagement.Api
dotnet run --project src/TaskManagement.Api
```

The app applies migrations on startup anyway, so the `ef database update` step above is
optional - just handy if you want the schema created up front.

Swagger UI: http://localhost:5080/swagger (or whichever port `dotnet run` prints).

### Tests

```bash
dotnet test
```

## Seeded admin credentials

- Email: `admin@example.com`
- Password: `Admin@123`

Set via `AdminSeed` in `appsettings.json`. Change these before deploying anywhere real.

## Auth

- `POST /api/auth/register` - anyone can register, always as role `User`. Returns an
  access token + refresh token.
- `POST /api/auth/login` - same, returns both tokens.
- `POST /api/auth/refresh` - exchanges a valid refresh token for a new pair. The old one
  gets revoked immediately, so it's useless even if it leaks after being used once.
- `POST /api/auth/logout` - revokes a refresh token.
- `GET /api/auth/me` - current user's profile.
- `GET/POST/DELETE /api/admin/users/*` - admin only.
- `POST/GET/PUT /api/tasks/*` - any authenticated user, but only for their own tasks.
  Hitting another user's task returns 404, not 403, so you can't tell the difference
  between "doesn't exist" and "exists but isn't yours."

In Swagger, click Authorize and paste just the token, no `Bearer ` prefix - Swashbuckle
adds that itself.

## Redis caching

`GET /api/tasks/{id}` is cached under `task:{id}` for 5 minutes. The first request loads
from SQL Server and writes to Redis; later requests come straight from Redis. Updating a
task's status refreshes that cache entry so nothing goes stale.

If Redis is down, reads/writes are caught and logged as warnings instead of failing the
request - it just falls back to hitting the database every time.

## Background processing

Creating a task saves it to the DB, then queues its ID on an in-memory `Channel<Guid>`. A
`BackgroundService` drains that channel, waits a few seconds to simulate real work, and
flips the task from `Pending` to `InProgress`, updating the cache to match.

## Business logic

- Tasks are sorted by priority first (High, Medium, Low), then by creation date within
  the same priority.
- Creating a task with the same title, for the same user, on the same day returns
  409 Conflict.

## Other stuff (bonus items)

- Global exception handling middleware - maps domain exceptions to proper status codes
  plus a `problem+json` body.
- Docker support (Dockerfile + docker-compose.yml).
- Unit tests for `TaskService` and `AuthService`.
- Serilog logging.
- Soft delete for users - an `IsDeleted` flag plus a global query filter, so deleted
  users disappear from listings/login without losing their data.
- Refresh tokens - random 64-byte tokens stored in a `RefreshTokens` table, 7-day expiry,
  rotated on use, revocable via logout.

## Assumptions

- SQL Server over PostgreSQL, since both were allowed. LocalDB/SQL Server Express for
  local dev, a real SQL Server container in Docker.
- "Sort by priority" means highest first.
- Duplicate detection is per-user, per-title, per calendar day (UTC), case-sensitive
  after trimming whitespace.
- Task ownership violations return 404 instead of 403, to avoid leaking other users'
  task IDs.
- The background worker moves tasks from Pending to InProgress, since the spec doesn't
  say what status they should land on.
- The JWT secret in `appsettings.json` is a placeholder - swap it for a real one outside
  local dev.
