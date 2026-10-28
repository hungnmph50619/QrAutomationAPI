# QrAutomationAPI — Playwright .NET 8 lab

ASP.NET Core Web API to learn persistent Chromium contexts, element screenshots,
QR-image refresh monitoring, cancellation, and cookie **metadata**.

**Scope:** This is a **localhost mock QR login**, not a Facebook QR login tool.
QR visuals are not scannable. No session cookie `Value` is exported.

## Requirements

- Windows with Visual Studio 2022 and .NET 8 SDK
- NuGet Microsoft.Playwright 1.64.1 (included in the project)
- Chromium installed for Playwright

Build from the repository root:

```powershell
dotnet restore .\QrAutomationAPI\QrAutomationAPI.csproj
dotnet build .\QrAutomationAPI\QrAutomationAPI.csproj
```

To install Chromium after building (PowerShell 7):

```powershell
pwsh .\QrAutomationAPI\bin\Debug\net8.0\playwright.ps1 install chromium
```

If `pwsh` is unavailable, use Windows PowerShell to run the generated script:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\QrAutomationAPI\bin\Debug\net8.0\playwright.ps1" install chromium
```

Start with the **https** launch profile in Visual Studio (Ctrl+F5).
Open https://localhost:7273/swagger (or the port shown by Visual Studio).

## Steps

1. `GET /api/automation/test-browser` — existing Chromium smoke test.
2. `POST /api/worker/start` — open visible Chromium at `/lab/login`.
3. Watch the simulated QR pattern change every 20 seconds.
4. `GET /api/worker/status` — count of screenshots taken every 12 seconds.
5. `GET /api/worker/logs` — recent log messages (last 100).
6. Click **Xác nhận đăng nhập demo** in the launched Chromium tab.
7. Wait until `GET /api/worker/status` becomes `Completed`.
8. Inspect `private_debug/qr.png` and `sessions/target_*.json`.
9. Instead of completing login, you can test `POST /api/worker/stop`.

The browser profile is persisted to `browser_profile_target/`. On each
`/lab/login` visit, the two demo cookies are reset for a fresh training cycle.

## Security

- New lab endpoints work only in **Development over HTTPS localhost**.
- The QR image is saved **outside wwwroot**, not published via an HTTP endpoint.
- Only `lab_user` and `lab_session` cookie **names and attributes**
  (domain, path, expires, Secure, HttpOnly, SameSite) are written to JSON.
- Never commit profile data, cookies, local screenshots, or secrets.
- Do not expose these developer-only endpoints on a public interface.
- The old `/api/automation/test-persistent` test uses the same profile:
  **do not run it at the same time as the worker**.

## Update from GitHub

Work on the `develop` branch. From the repository root:

```powershell
git switch develop
git pull --ff-only
dotnet build .\QrAutomationAPI\QrAutomationAPI.csproj
```

Stop and restart the ASP.NET Core application after pulling changes.

## Notes

Playwright operations already in progress may wait for their timeout before
responding to cancellation. The 12-second interval is a polling interval and
does not guarantee that a simulated QR changes at that exact moment.
