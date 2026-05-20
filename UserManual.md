# User Manual

## Prerequisites

- .NET 8 SDK (8.0.401+)
- .NET 9 MAUI workload (`dotnet workload install maui`)
- Windows 10+ for WPF DesktopApp
- macOS 12+ for MAUI Mac Catalyst app

## 1. Start the Ingestion API

The API must be running before starting any client app or the simulator.

```bash
cd src/IngestionApi
dotnet run
```

**Expected output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5100
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Verify it's running:**
```bash
curl http://localhost:5100/healthz
# {"status":"healthy"}
```

## 2. Start the Device Simulator

The simulator posts HeartRate measurements to the API every 2 seconds.

```bash
cd src/DeviceSimulator
dotnet run
```

**Expected output:**
```
DeviceSimulator started. Posting measurements every 2s...
[10:30:00] Posted HeartRate=72 bpm → 202
[10:30:02] Posted HeartRate=85 bpm → 202
[10:30:04] Posted HeartRate=68 bpm → 202
```

Press `Ctrl+C` to stop.

## 3. Start the MAUI App (macOS or Windows)

### macOS (Mac Catalyst)

```bash
cd src/MauiApp
dotnet build -f net9.0-maccatalyst
open bin/Debug/net9.0-maccatalyst/maccatalyst-arm64/MauiApp.app
```

### Windows

```bash
cd src/MauiApp
dotnet run -f net9.0-windows10.0.19041.0
```

The app auto-polls the API every second and displays live measurements. Click **Refresh** for a manual refresh.

## 4. Start the WPF Desktop App (Windows only)

```bash
cd src/DesktopApp
dotnet run
```

The Vitals Monitor window opens, auto-polls every second, and displays measurements in a data grid. Click **Refresh** for a manual refresh.

## Running All Together

For the full live pipeline, open three terminals and run in order:

| Terminal | Command | Purpose |
|----------|---------|---------|
| 1 | `dotnet run --project src/IngestionApi` | Start API on port 5100 |
| 2 | `dotnet run --project src/DeviceSimulator` | Post measurements every 2s |
| 3 | Build & open MAUI app (or `dotnet run --project src/DesktopApp` on Windows) | Display live data |

## Configuration

All apps use a static HTTP port and API key:

| Setting | Value |
|---------|-------|
| API URL | `http://localhost:5100` |
| API Key Header | `x-api-key: local-dev` |

## Troubleshooting

| Issue | Solution |
|-------|----------|
| MAUI app shows no data | Ensure the API is running on port 5100 |
| DeviceSimulator connection refused | Start the API first |
| WPF app won't build on macOS | WPF requires Windows; use the MAUI app on macOS |
| MAUI HTTP blocked on macOS | Already configured via `NSAllowsLocalNetworking` in Info.plist |
