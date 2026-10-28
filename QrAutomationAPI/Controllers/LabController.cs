using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace QrAutomationAPI.Controllers;

// Bài lab localhost: QR giả lập, không liên quan Facebook hay tài khoản thật.
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("lab")]
public sealed class LabController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    public LabController(IWebHostEnvironment env) => _env = env;

    private bool IsLocalHttps()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        var host = Request.Host.Host;
        return _env.IsDevelopment()
            && Request.IsHttps
            && (ip is null || IPAddress.IsLoopback(ip))
            && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host == "127.0.0.1");
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (!IsLocalHttps()) return NotFound();

        // Mỗi lần khởi động lab xóa cookie demo cũ để phiên trước
        // không khiến worker báo hoàn thành ngay khi mở trang.
        Response.Cookies.Delete("lab_user", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("lab_session", new CookieOptions { Path = "/" });
        return Content(Html, "text/html; charset=utf-8");
    }

    [HttpPost("complete")]
    public IActionResult Complete()
    {
        if (!IsLocalHttps()) return NotFound();

        // Chỉ nhận yêu cầu từ chính trang lab trên cùng origin.
        var origin = Request.Headers.Origin.ToString();
        var expectedOrigin = $"{Request.Scheme}://{Request.Host}";
        if (!string.Equals(origin, expectedOrigin, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        };
        Response.Cookies.Append("lab_user", "training", options);
        Response.Cookies.Append("lab_session", "DEMO_ONLY", options);
        return Ok(new { Success = true, Message = "Đăng nhập demo hoàn tất" });
    }

    private const string Html = """
<!doctype html>
<html lang="vi">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>Playwright QR Lab</title>
<style>
 body {font-family:Arial,sans-serif;background:#f0f4f9;color:#1b2539;min-height:100vh;display:grid;place-items:center;margin:0}
 main {background:white;padding:30px;border-radius:16px;box-shadow:0 8px 28px #0002;text-align:center;max-width:450px}
 .qr {margin:20px auto;padding:12px;width:220px;height:220px;border:1px solid #cbd5e1;border-radius:10px;display:grid;place-items:center}
 canvas {width:200px;height:200px;image-rendering:pixelated}
 button {background:#3059bb;color:#fff;padding:13px 21px;border:0;border-radius:8px;cursor:pointer;font-weight:bold}
 small {color:#667085} #message{font-weight:bold;color:#12623d}
</style>
</head>
<body>
<main>
<h2>QR Login Lab (mô phỏng)</h2>
<p>Hình thay đổi sau mỗi 20 giây. <strong>Không phải mã QR đăng nhập thật.</strong></p>
<div class="qr" id="demo-qr" data-testid="login-qr">
 <canvas id="pattern" width="200" height="200" aria-label="Hình QR mô phỏng"></canvas>
</div>
<p id="status">Đang tạo ảnh mẫu...</p>
<button type="button" id="complete">Xác nhận đăng nhập demo</button>
<p id="message"></p>
<small>Chỉ sử dụng để học Playwright, cookie và vòng đời phiên.</small>
</main>
<script>
 const canvas=document.querySelector('#pattern');
 const ctx=canvas.getContext('2d');
 let version=0;
 function paint(){
   version++;
   ctx.fillStyle='white'; ctx.fillRect(0,0,200,200);
   const cell=6, offset=13;
   function square(x,y,n,color){
     ctx.fillStyle=color;
     ctx.fillRect(offset+x*cell,offset+y*cell,n*cell,n*cell);
   }
   function finder(x,y){
     square(x,y,7,'#111827');
     square(x+1,y+1,5,'white');
     square(x+2,y+2,3,'#111827');
   }
   for(let y=0;y<29;y++){
     for(let x=0;x<29;x++){
       const reserved=(x<8&&y<8)||(x>=21&&y<8)||(x<8&&y>=21);
       if(!reserved && Math.random()>.5) square(x,y,1,'#111827');
     }
   }
   finder(0,0);finder(22,0);finder(0,22);
   document.querySelector('#status').textContent='Mẫu số '+version+' — đổi sau 20 giây';
 }
 paint();setInterval(paint,20000);
 document.querySelector('#complete').addEventListener('click',async()=>{
   const res=await fetch('/lab/complete',{method:'POST'});
   document.querySelector('#message').textContent=res.ok
     ? 'Đã đặt cookie demo. Worker sẽ tự xác nhận.'
     : 'Lỗi đăng nhập demo.';
 });
</script>
</body></html>
""";
}
