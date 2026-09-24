using System.Text;
using System.Text.Json.Serialization;
using FineManagement.Api.Data;
using FineManagement.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
// 日志写入程序目录下的 logs 文件夹，每天生成一个文件并保留最近 30 天。
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "FineManagement.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(logDirectory, "backend-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1),
        encoding: Encoding.UTF8,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog(Log.Logger, dispose: true);

try
{
    // Web API 使用字符串枚举，确保前端收到 Pending、Completed 等稳定值。
    builder.Services
        .AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection configuration.");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySQL(connectionString));

    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IExcelService, ExcelService>();

    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Missing Jwt:Key configuration.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

    builder.Services.AddAuthorization();
    // 当前内网部署允许任意来源访问 API；如部署到公网，应改成明确的前端域名白名单。
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "考核管理系统 API",
            Version = "v1",
            Description = "考核记录、部门、收款方式与用户管理接口"
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "输入登录接口返回的 JWT Token"
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = Array.Empty<string>()
        });

        // 将控制器 XML 注释显示在 Swagger 接口说明中。
        var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
    });

    var app = builder.Build();

    // 记录请求耗时、状态码、远端地址及登录用户名，便于部署后排查接口问题。
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, _, exception) =>
            exception is not null || httpContext.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : httpContext.Response.StatusCode >= 400
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("UserName", httpContext.User.Identity?.Name ?? "anonymous");
        };
    });

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "考核管理系统 API v1");
        options.RoutePrefix = "swagger";
    });

    app.UseHttpsRedirection();
    app.UseCors("Frontend");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // 首次运行创建数据库结构并补齐兼容字段；可通过配置关闭启动初始化。
    if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
    {
        await DbInitializer.InitializeAsync(app.Services);
    }

    Log.Information("考核管理系统后端启动，日志目录：{LogDirectory}", logDirectory);
    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "考核管理系统后端异常终止");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
