using QrAutomationAPI.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Web API và Swagger giữ nguyên cấu hình của project đang chạy.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Một worker duy nhất tránh hai Chromium dùng chung persistent profile.
builder.Services.AddSingleton<QrWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Trang quản lý chỉ mở tại HTTPS localhost trong môi trường Development.
    // Không bật static-file middleware công khai thư mục wwwroot.
    app.MapGet("/", (HttpContext ctx, IWebHostEnvironment env) =>
    {
        var ip = ctx.Connection.RemoteIpAddress;
        var host = ctx.Request.Host.Host;
        bool local = ctx.Request.IsHttps
            && ip is not null && IPAddress.IsLoopback(ip)
            && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host == "127.0.0.1");
        if (!local) return (IResult)Results.NotFound();

        ctx.Response.Headers.CacheControl = "private, no-store";
        return (IResult)Results.File(
            Path.Combine(env.ContentRootPath, "wwwroot", "index.html"),
            "text/html; charset=utf-8");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
