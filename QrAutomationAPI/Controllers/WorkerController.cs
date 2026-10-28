using Microsoft.AspNetCore.Mvc;
using QrAutomationAPI.Services;
using System.Net;

namespace QrAutomationAPI.Controllers;

[ApiController]
[Route("api/worker")]
public sealed class WorkerController : ControllerBase
{
    private readonly QrWorker _worker;
    private readonly IWebHostEnvironment _env;

    public WorkerController(QrWorker worker, IWebHostEnvironment env)
    {
        _worker = worker;
        _env = env;
    }

    // Tất cả endpoint của lab chỉ chạy trong Development qua HTTPS localhost.
    private bool IsLocalHttps()
    {
        var host = Request.Host.Host;
        var ip = HttpContext.Connection.RemoteIpAddress;
        return _env.IsDevelopment()
            && Request.IsHttps
            && (ip is null || IPAddress.IsLoopback(ip))
            && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host == "127.0.0.1");
    }

    // Ngăn website ngoài kích hoạt Start/Stop bằng cross-site form.
    private bool SameOriginPost()
    {
        var origin = Request.Headers.Origin.ToString();
        var site = Request.Headers["Sec-Fetch-Site"].ToString();
        var expected = $"{Request.Scheme}://{Request.Host}";
        return (string.IsNullOrWhiteSpace(origin)
                || string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(site) || site is "same-origin" or "none");
    }

    [HttpPost("start")]
    public IActionResult Start()
    {
        if (!IsLocalHttps() || !SameOriginPost()) return NotFound();
        string origin = $"{Request.Scheme}://{Request.Host}";
        if (!_worker.TryStart(origin))
            return Conflict(new { Message = "Worker đang chạy hoặc đang dừng" });

        return Accepted(new
        {
            Message = "Đã yêu cầu khởi động Chromium và QR demo",
            StatusUrl = "/api/worker/status"
        });
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        if (!IsLocalHttps() || !SameOriginPost()) return NotFound();
        return _worker.RequestStop()
            ? Accepted(new { Message = "Đã gửi yêu cầu dừng" })
            : Ok(new { Message = "Không có worker đang chạy" });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        if (!IsLocalHttps()) return NotFound();
        return Ok(_worker.GetStatus());
    }

    // Chỉ trả về ảnh QR giả lập của bài lab, qua HTTPS localhost.
    // File không đặt trong wwwroot và không được cache.
    [HttpGet("qr-preview")]
    public IActionResult QrPreview()
    {
        if (!IsLocalHttps()) return NotFound();
        var path = Path.Combine(_env.ContentRootPath, "private_debug", "qr.png");
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, "image/png");
    }

    [HttpGet("logs")]
    public IActionResult Logs()
    {
        if (!IsLocalHttps()) return NotFound();
        return Ok(_worker.GetLogs());
    }
}
