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

    [HttpPost("start")]
    public IActionResult Start()
    {
        if (!IsLocalHttps()) return NotFound();
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
        if (!IsLocalHttps()) return NotFound();
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

    [HttpGet("logs")]
    public IActionResult Logs()
    {
        if (!IsLocalHttps()) return NotFound();
        return Ok(_worker.GetLogs());
    }
}
