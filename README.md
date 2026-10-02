# PawStash

A file manager for links, notes and photos: .NET MAUI client + ASP.NET Core API + PostgreSQL.

## Toolchain (important)

This machine has **two** .NET SDKs:

| SDK | Location | Notes |
|---|---|---|
| 8.0.402 | `C:\Program Files\dotnet` | Pre-existing, system-wide. Has **no** MAUI workload. |
| 10.0.400 | `%USERPROFILE%\.dotnet` | Installed for this project, with the `maui-windows` workload. **This is the one you want.** |

The account is not an administrator, so the SDK was installed per-user. `C:\Program Files\dotnet`
sits in the *machine* PATH, which Windows always resolves **before** the user PATH — so typing a bare
`dotnet` gets you 8.0.402, which cannot build this project. `global.json` pins the SDK to 10.0.400 so
this fails loudly ("A compatible .NET SDK was not found") rather than doing something surprising.

Three ways to get the right SDK:

1. **`build.cmd` / `run.cmd`** in this folder — they set `DOTNET_ROOT` and call the right `dotnet.exe`.
2. **VS Code's integrated terminal** — `.vscode/settings.json` prepends the correct SDK, so plain
   `dotnet build` works there.
3. **Full path** — `"%USERPROFILE%\.dotnet\dotnet.exe" build`

`DOTNET_ROOT` was deliberately *not* set user-wide: it would point .NET 8 apps at a root containing
no 8.x runtime and break them.

## Layout

Layered like SalvageWorks' `sw-api`, deliberately smaller: four server projects, no extension libraries.

| Path | What |
|---|---|
| `pawstash-api/PawStash.API` | Controllers (`BaseApiController.ResolveResponse`), `Bootstrapper.cs`, `Program.cs`, appsettings |
| `pawstash-api/PawStash.BLL` | `Interfaces/` + `Implementations/` (services), `Validators/` (FluentValidation), `Results/ServiceResult`, `ServiceRegistration.cs` |
| `pawstash-api/PawStash.DAL` | `Context/` (`PawStashContext` + `IPawStashContext`), `Entities/`, `EntityTypeConfigurations/`, `Migrations/` |
| `pawstash-api/PawStash.Common` | `Models/DTO/<Feature>/`, `Rules/` (validation shared with the app). No server libraries: the app references it |
| `pawstash-app/PawStash.App` | .NET MAUI client (Windows now, Android later) |
| `docker-compose.yml` | PostgreSQL for local development |

References: API → BLL, DAL · BLL → DAL, Common · DAL → Common · App → Common.

A request flows `AuthController` → `IAuthService` (validates with `LoginPostDtoValidator`, queries
`IPawStashContext`) → `ServiceResult<T>` → `ResolveResponse` turns it into 200 / 400 / 401 / 404 / 409
(errors as standard ProblemDetails).

Not taken from SalvageWorks, on purpose: Evolve and the separate DatabaseMigrator (EF migrations apply on
startup instead), the `{ data, errors }` response wrapper, AutoMapper, generic repositories.

Database names are lowercase snake_case with descriptive names (`users.email`). Server code follows
`pawstash-api/.editorconfig` (copied from `sw-api`): explicit types, block-scoped namespaces, no comments.

Swagger UI (Development only): http://localhost:5094/swagger. Every endpoint except login needs an
allowed email in the `X-User-Email` header: click **Authorize** in Swagger and enter one (for example
`test@test.com`).

## Docs

Plans and design notes live in [`docs/`](docs/):

- [`file-system-integration-plan.md`](docs/file-system-integration-plan.md) — folders, notes, links and photos: schema, layers, API, stages

## Build and run

Needs Docker running (for PostgreSQL).

```cmd
run-api.cmd        :: starts PostgreSQL + API on http://localhost:5094 (applies migrations)
run.cmd            :: in a second terminal: launches the Windows app
build.cmd          :: compile everything without running
```

## Sign-in

Email only, no password yet. The API accepts an email if it is in the `users` table (one column, `email`,
which is also the primary key). The initial migration seeds `1979stetsenko@gmail.com`. Emails are
trimmed and lowercased before lookup, so store them lowercase.

For quick testing, `test@test.com` is also allowed **in development only**. It is set as `DevTestEmail`
in `pawstash-api/PawStash.API/appsettings.Development.json`, and the API inserts it on startup when running in
Development (`Bootstrapper.PrepareDatabaseAsync`). It is deliberately not in a migration, because migrations also run on a hosted server, where a
well-known email would let anyone in. If you ever copy the local database to a server, delete that row.

Add another allowed email:

```cmd
docker exec pawstash-db psql -U pawstash -c "insert into users (email) values ('someone@example.com')"
```

The app remembers the signed-in email on the device (MAUI `Preferences`) and opens straight to the
home page next launch. This is identification, not security: anyone who knows an allowed email can sign in.

## Migrations

EF Core migrations live in `PawStash.DAL/Migrations` and are applied automatically when the API starts.
After changing an entity or its configuration:

```cmd
dotnet tool restore
dotnet dotnet-ef migrations add <MeaningfulName> -p pawstash-api\PawStash.DAL -s pawstash-api\PawStash.API -o Migrations
```

(Use the SDK from `%USERPROFILE%\.dotnet`, see Toolchain above.) Read the generated migration before
running the API. The history table is `ef_migrations_history`; its columns keep EF's own names.

## Adding Android later

1. Uncomment the Android line in `pawstash-app/PawStash.App/PawStash.App.csproj`:

   ```xml
   <TargetFrameworks>$(TargetFrameworks);net10.0-android</TargetFrameworks>
   ```

2. Install **JDK 17**. The system JDK is 1.8, which is too old for modern Android Gradle.
   Microsoft OpenJDK 17 has a per-user zip that needs no admin rights.

3. Install the workload:

   ```cmd
   "%USERPROFILE%\.dotnet\dotnet.exe" workload install maui-android
   ```

4. Let the SDK fetch the Android SDK and accept licenses:

   ```cmd
   "%USERPROFILE%\.dotnet\dotnet.exe" build -t:InstallAndroidDependencies -f net10.0-android ^
     -p:AndroidSdkDirectory="%USERPROFILE%\android-sdk" ^
     -p:JavaSdkDirectory="<path-to-jdk-17>" ^
     -p:AcceptAndroidSDKLicenses=True
   ```

Budget roughly 5-10 GB of downloads.

## Known gotcha

Do **not** add `<MauiXamlInflator>SourceGen</MauiXamlInflator>` to the csproj. On .NET 10 it also
claims `Platforms/Windows/App.xaml` (a WinUI file, not a MAUI one). The WinUI markup compiler still
writes `App.g.i.cs` — which holds `Main` and `InitializeComponent` — but it never reaches the
compiler, and the build dies with `CS5001` and `CS1061`. The template ships this property enabled;
it was removed here. There is a comment in `pawstash-app/PawStash.App/PawStash.App.csproj` marking the spot.
