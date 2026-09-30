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

| Path | What |
|---|---|
| `src/PawStash.App` | .NET MAUI client (Windows now, Android later) |
| `src/PawStash.Api` | ASP.NET Core API + EF Core (PostgreSQL) |
| `src/PawStash.Shared` | Code both sides use: request/response types, `EmailRules` validation |
| `docker-compose.yml` | PostgreSQL for local development |

## Build and run

Needs Docker running (for PostgreSQL).

```cmd
run-api.cmd        :: starts PostgreSQL + API on http://localhost:5094 (applies migrations)
run.cmd            :: in a second terminal: launches the Windows app
build.cmd          :: compile everything without running
```

## Sign-in

Email only, no password yet. The API accepts an email if it is in the `Users` table (one column, `Email`,
which is also the primary key). The initial migration seeds `1979stetsenko@gmail.com`. Emails are
trimmed and lowercased before lookup, so store them lowercase.

Add another allowed email:

```cmd
docker exec pawstash-db psql -U pawstash -c "insert into \"Users\" values ('someone@example.com')"
```

The app remembers the signed-in email on the device (MAUI `Preferences`) and opens straight to the
home page next launch. This is identification, not security: anyone who knows an allowed email can sign in.

## Migrations

```cmd
dotnet tool restore
dotnet dotnet-ef migrations add <Name> -p src\PawStash.Api -o Data\Migrations
```

(Use the SDK from `%USERPROFILE%\.dotnet`, see Toolchain above.)

## Adding Android later

1. Uncomment the Android line in `src/PawStash.App/PawStash.csproj`:

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
it was removed here. There is a comment in `PawStash.csproj` marking the spot (`src/PawStash.App/PawStash.csproj`).
