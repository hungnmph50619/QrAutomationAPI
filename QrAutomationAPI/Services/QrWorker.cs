using Microsoft.Playwright;
using System.Security.Cryptography;
using System.Text.Json;

namespace QrAutomationAPI.Services;

/// <summary>
/// Quản lý duy nhất một Chromium persistent profile để thử nghiệm QR trên localhost.
/// Không lấy cookie/value hay QR đăng nhập của dịch vụ bên ngoài.
/// </summary>
public sealed class QrWorker : IAsyncDisposable
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<QrWorker> _logger;
    private readonly object _sync = new();
    private readonly Queue<string> _logs = new();
    private CancellationTokenSource? _cts;
    private Task? _task;
    private string _state = "Idle";
    private string _message = "Chưa khởi động";
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _lastCheckUtc;
    private int _screenshotCount;
    private bool? _lastQrChanged;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(12);

    public QrWorker(IWebHostEnvironment env, ILogger<QrWorker> logger)
    {
        _env = env;
        _logger = logger;
    }

    public bool TryStart(string origin)
    {
        lock (_sync)
        {
            // Không cho phép hai Chromium mở chung một user data directory.
            if (_task is { IsCompleted: false }) return false;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _state = "Starting";
            _message = "Đang mở Chromium";
            _startedAtUtc = DateTimeOffset.UtcNow;
            _lastCheckUtc = null;
            _screenshotCount = 0;
            _lastQrChanged = null;
            _logs.Clear();
            // Trả HTTP ngay, worker hoạt động bất đồng bộ.
            _task = Task.Run(() => RunAsync(origin, token));
            return true;
        }
    }

    public bool RequestStop()
    {
        lock (_sync)
        {
            if (_task is not { IsCompleted: false }) return false;
            _state = "Stopping";
            _message = "Đã yêu cầu dừng";
            _cts?.Cancel();
            return true;
        }
    }

    public object GetStatus()
    {
        lock (_sync)
        {
            return new
            {
                State = _state, Message = _message,
                StartedAtUtc = _startedAtUtc,
                LastCheckUtc = _lastCheckUtc,
                ScreenshotCount = _screenshotCount,
                LastQrChanged = _lastQrChanged,
                IntervalSeconds = 12, QrRefreshSeconds = 20
            };
        }
    }

    public string[] GetLogs()
    {
        lock (_sync) return _logs.ToArray();
    }

    private void Report(string state, string message)
    {
        lock (_sync)
        {
            _state = state;
            _message = message;
            _logs.Enqueue($"{DateTimeOffset.Now:HH:mm:ss} [{state}] {message}");
            while (_logs.Count > 100) _logs.Dequeue();
        }
        _logger.LogInformation("QrWorker {State}: {Message}", state, message);
    }

    private async Task RunAsync(string origin, CancellationToken token)
    {
        IPlaywright? playwright = null;
        IBrowserContext? context = null;

        var root = _env.ContentRootPath;
        var profileDir = Path.Combine(root, "browser_profile_target");
        var privateDir = Path.Combine(root, "private_debug");
        var sessionDir = Path.Combine(root, "sessions");
        var qrPath = Path.Combine(privateDir, "qr.png");

        try
        {
            Directory.CreateDirectory(profileDir);
            Directory.CreateDirectory(privateDir);
            Directory.CreateDirectory(sessionDir);

            token.ThrowIfCancellationRequested();
            playwright = await Playwright.CreateAsync();

            // Bật cửa sổ để người học bấm nút đăng nhập demo.
            context = await playwright.Chromium.LaunchPersistentContextAsync(
                profileDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    IgnoreHTTPSErrors = true, // Chỉ chấp nhận HTTPS localhost trong lab.
                    ViewportSize = new() { Width = 1280, Height = 800 }
                });

            token.ThrowIfCancellationRequested();
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
            await page.GotoAsync(origin + "/lab/login", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });
            Report("Running", "Đã mở trang QR mô phỏng trên HTTPS localhost");

            string? previousHash = null;
            while (true)
            {
                token.ThrowIfCancellationRequested();

                // Chỉ kiểm tra tên hai cookie DEMO. Không đọc hoặc ghi giá trị.
                var cookies = await context.CookiesAsync(new[] { origin });
                bool loggedIn = cookies.Any(c => c.Name == "lab_user")
                    && cookies.Any(c => c.Name == "lab_session");
                lock (_sync) _lastCheckUtc = DateTimeOffset.UtcNow;

                if (loggedIn)
                {
                    await SaveCookieMetadataAsync(context, origin, sessionDir, token);
                    Report("Completed", "Đã xác nhận phiên demo và lưu metadata cookie");
                    return;
                }

                // Dùng selector chính và fallback để học Locator.
                string[] selectors =
                {
                    "[data-testid='login-qr']",
                    "#demo-qr",
                    ".qr"
                };
                bool captured = false;
                foreach (var selector in selectors)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var qr = page.Locator(selector).First;
                        if (!await qr.IsVisibleAsync()) continue;

                        var imageBytes = await qr.ScreenshotAsync(new LocatorScreenshotOptions
                        {
                            Path = qrPath,
                            Timeout = 3000,
                            Animations = ScreenshotAnimations.Disabled
                        });
                        var hash = Convert.ToHexString(SHA256.HashData(imageBytes));
                        bool? changed = previousHash is null ? null : hash != previousHash;
                        previousHash = hash;
                        lock (_sync)
                        {
                            _screenshotCount++;
                            _lastQrChanged = changed;
                        }
                        var result = changed switch
                        {
                            null => "Ảnh QR mô phỏng đầu tiên",
                            true => "Hình QR mô phỏng đã thay đổi",
                            false => "Hình QR mô phỏng chưa thay đổi"
                        };
                        Report("Running", result + " — private_debug/qr.png");
                        captured = true;
                        break;
                    }
                    catch (PlaywrightException)
                    {
                        // Element có thể vừa đổi; thử selector kế tiếp.
                    }
                }

                if (!captured) Report("Running", "Chưa tìm thấy phần tử QR mô phỏng");
                await Task.Delay(PollInterval, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Report("Stopped", "Đã dừng theo CancellationToken");
        }
        catch (Exception ex)
        {
            Report("Error", "Lỗi Playwright: " + ex.Message);
            _logger.LogError(ex, "QrWorker gặp lỗi");
        }
        finally
        {
            if (context is not null)
            {
                try { await context.CloseAsync(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Lỗi khi đóng Chromium"); }
            }
            playwright?.Dispose();
        }
    }

    private static async Task SaveCookieMetadataAsync(
        IBrowserContext context, string origin, string outputDir, CancellationToken token)
    {
        // Chỉ lưu metadata thuộc lab trên localhost, KHÔNG lưu Value.
        var cookies = await context.CookiesAsync(new[] { origin });
        var metadata = cookies
            .Where(c => c.Name is "lab_user" or "lab_session")
            .Select(c => new
            {
                c.Name, c.Domain, c.Path, c.Expires,
                c.HttpOnly, c.Secure,
                SameSite = c.SameSite.ToString()
            }).ToArray();

        var report = new
        {
            LoggedInDemo = true,
            CheckedAtUtc = DateTimeOffset.UtcNow,
            CookieCount = metadata.Length,
            Cookies = metadata
        };
        var file = Path.Combine(outputDir, $"target_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.json");
        await File.WriteAllTextAsync(file,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            token);
    }

    public async ValueTask DisposeAsync()
    {
        Task? task;
        CancellationTokenSource? cts;
        lock (_sync)
        {
            cts = _cts;
            task = _task;
            cts?.Cancel();
        }
        if (task is not null)
        {
            try { await task; }
            catch (OperationCanceledException) { }
        }
        cts?.Dispose();
    }
}
