# EventTimings

EventTimings is a .NET Blazor WebAssembly app for timing a cycling event on phones or tablets, backed by an ASP.NET Core API and shared contracts. The current implementation is shaped around the SFA Sportive 2026 event and is intended to evolve into a cloud-hosted event timing tool with Azure SQL and offline-friendly client behavior.

## Solution layout

- `src/EventTimings.App` - Blazor WebAssembly PWA client
- `src/EventTimings.Api` - ASP.NET Core API for event timing operations
- `src/EventTimings.Contracts` - shared DTOs and event timing contracts

## Current state

- Mobile-first timing board for the SFA Sportive 2025 event
- Start and stop timing actions for officials
- Seeded in-memory event data for local development
- Local fallback behavior when the API is unavailable
- SFA-inspired theme and app shell styling

## Run locally

1. Build the solution.

```powershell
dotnet build EventTimings.slnx
```

2. Start the API project.

```powershell
dotnet run --project src/EventTimings.Api/EventTimings.Api.csproj
```

3. Start the Blazor client in a second terminal.

```powershell
dotnet run --project src/EventTimings.App/EventTimings.App.csproj
```

4. Open the client in the browser and use the default official credentials.

## Default development values

- Official name: `Mark Phillipson`
- PIN: `****`
- Event code: `marden-2026`

## Next steps

- Replace the in-memory store with Azure SQL Database
- Add persistent synchronization for offline timing actions
- Add an admin screen foer event and rider setup
- Add deployment configuration for Azure hosting

```powershell
dotnet publish src/EventTimings.Api/EventTimings.Api.csproj -c Release -o publish_output/EventTimings.Api
dotnet publish src/EventTimings.App/EventTimings.App.csproj -c Release -o publish_output/EventTimings.App
azd config show --output json
azd up --output json
```
