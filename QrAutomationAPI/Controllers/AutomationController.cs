
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;

namespace QrAutomationAPI.Controllers
{
    [ApiController]
    [Route("api/automation")]
    public class AutomationController : ControllerBase
    {
        [HttpGet("test-browser")]
        public async Task<IActionResult> TestBrowser()
        {
            try
            {
                // Khởi tạo Playwright
                using var playwright =
                    await Playwright.CreateAsync();

                // Mở Chromium ở chế độ headless
                await using var browser =
                    await playwright.Chromium.LaunchAsync(
                        new BrowserTypeLaunchOptions
                        {
                            Headless = true
                        });

                // Tạo trang trình duyệt mới
                var page = await browser.NewPageAsync();

                // Tạo trang HTML thử nghiệm nội bộ
                // Không cần kết nối Internet
                await page.SetContentAsync(
                    "<html><head><title>Playwright OK</title>" +
                    "</head><body>Browser works!</body></html>");

                // Đọc tiêu đề để kiểm tra Chromium
                string title = await page.TitleAsync();

                return Ok(new
                {
                    Success = true,
                    Browser = "Chromium",
                    Title = title,
                    Message = "Playwright chạy thành công!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpGet("test-persistent")]
        public async Task<IActionResult> TestPersistent()
        {
            // Tạo thư mục lưu profile trình duyệt
            string profilePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "browser_profile_target");

            Directory.CreateDirectory(profilePath);

            using var playwright =
                await Playwright.CreateAsync();

            // Mở Chromium bằng Persistent Context
            var context = await playwright.Chromium
                .LaunchPersistentContextAsync(
                    profilePath,
                    new BrowserTypeLaunchPersistentContextOptions
                    {
                        Headless = true,
                        ViewportSize = new()
                        {
                            Width = 1280,
                            Height = 800
                        }
                    });

            try
            {
                var page = context.Pages.FirstOrDefault()
                    ?? await context.NewPageAsync();

                await page.GotoAsync("https://example.com");

                string title = await page.TitleAsync();

                return Ok(new
                {
                    Success = true,
                    PersistentContext = true,
                    PageTitle = title,
                    ProfilePath = profilePath,
                    Message = "Persistent Context hoạt động!"
                });
            }
            finally
            {
                // Đóng Chromium nhưng giữ profile
                await context.CloseAsync();
            }
        }

    }
}
