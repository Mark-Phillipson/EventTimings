Playwright E2E tests (Playwright for .NET)

Prerequisites
- .NET SDK (matching the solution; tests target `net10.0`)
- Playwright browsers installed for .NET. Install the Playwright CLI and browsers:

  dotnet tool install --global Microsoft.Playwright.CLI
  playwright install

Run tests

- Restore and build:

  dotnet restore
  dotnet build

- Run tests (optionally set `E2E_BASE_URL`):

  set E2E_BASE_URL=https://localhost:7041
  dotnet test PlaywrightTests/PlaywrightTests.csproj

Notes
- The tests expect the app to be running at `E2E_BASE_URL` (default: https://localhost:7041). Adjust as needed.
- The provided test is a minimal scaffold that verifies the login modal is shown on first load. Expand tests to cover login, gating, and timing flows.
