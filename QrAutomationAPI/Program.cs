using QrAutomationAPI.Services;

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
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
