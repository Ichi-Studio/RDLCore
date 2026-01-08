using System.Reflection;
using System.Text.Json.Serialization;
using RdlCore.Agent;
using RdlCore.Generation;
using RdlCore.Logic;
using RdlCore.Parsing;
using RdlCore.Rendering;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RDL-Core API",
        Version = "v1",
        Description = """
            **RDL-Core** - 智能报表转换引擎 API
            
            将 Word/PDF 文档转换为 RDLC 报表定义文件。
            
            ## 主要功能
            
            - **文档转换** - 上传 .docx 或 .pdf 文件，自动转换为 .rdlc 格式
            - **文档分析** - 分析文档结构，提取字段代码和逻辑表达式
            - **RDLC 验证** - 验证 RDLC 文件的 Schema 合规性和表达式正确性
            
            ## 支持的格式
            
            | 输入格式 | 说明 |
            |----------|------|
            | .docx | Microsoft Word 文档 (OpenXML) |
            | .pdf | PDF 文档 |
            
            | 输出格式 | 说明 |
            |----------|------|
            | .rdlc | Report Definition Language Client (RDL 2016) |
            
            ## 表达式转换
            
            支持将 Word 字段代码自动转换为 RDL VBScript 表达式：
            
            - `{ MERGEFIELD CustomerName }` → `=Fields!CustomerName.Value`
            - `{ IF ... }` → `=IIf(...)`
            - `{ PAGE }` → `=Globals!PageNumber`
            """,
        Contact = new OpenApiContact
        {
            Name = "RDL-Core Team",
            Email = "support@rdlcore.dev"
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Enable XML comments
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Support file upload
    options.OperationFilter<FileUploadOperationFilter>();
});

// Configure core services
builder.Services.Configure<AxiomRdlCoreOptions>(builder.Configuration.GetSection("AxiomRdlCore"));

builder.Services.AddRdlCoreParsing();
builder.Services.AddRdlCoreLogic();
builder.Services.AddRdlCoreGeneration();
builder.Services.AddRdlCoreRendering();
builder.Services.AddRdlCoreAgent();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure middleware pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RDL-Core API v1");
    options.RoutePrefix = "swagger"; // Swagger UI at /swagger
    options.DocumentTitle = "RDL-Core API";
    options.DefaultModelsExpandDepth(-1); // Hide schemas by default
    options.EnableDeepLinking();
    options.DisplayRequestDuration();
});

app.UseCors();

// Serve static files (Pixel UI)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

// Fallback to index.html for SPA
app.MapFallbackToFile("index.html");

// Print startup info
var urls = app.Urls.Any() ? string.Join(", ", app.Urls) : "http://localhost:5000";
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║      ____  ____  __          ______                           ║");
Console.WriteLine("║     / __ \\/ __ \\/ /         / ____/___  ________             ║");
Console.WriteLine("║    / /_/ / / / / /   ______/ /   / __ \\/ ___/ _ \\            ║");
Console.WriteLine("║   / _, _/ /_/ / /___/_____/ /___/ /_/ / /  /  __/            ║");
Console.WriteLine("║  /_/ |_|\\____/_____/      \\____/\\____/_/   \\___/             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║              RDL-Core API v1.0.0                             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║                                                              ║");
Console.WriteLine($"║  Pixel UI:    {urls + "/index.html",-47} ║");
Console.WriteLine($"║  Swagger UI:  {urls + "/swagger",-47} ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Endpoints:                                                  ║");
Console.WriteLine("║    POST /api/convert          - 转换文档到 RDLC              ║");
Console.WriteLine("║    POST /api/convert/analyze  - 分析文档结构                 ║");
Console.WriteLine("║    POST /api/convert/download - 下载 RDLC 文件               ║");
Console.WriteLine("║    POST /api/validate         - 验证 RDLC 文件               ║");
Console.WriteLine("║    GET  /api/health           - 健康检查                     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

app.Run();

/// <summary>
/// Swagger 文件上传操作过滤器
/// </summary>
public class FileUploadOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        var fileParams = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile));

        if (!fileParams.Any()) return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = fileParams.ToDictionary(
                            p => p.Name!,
                            p => new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary",
                                Description = "文件 (.docx 或 .pdf)"
                            }),
                        Required = fileParams.Select(p => p.Name!).ToHashSet()
                    }
                }
            }
        };
    }
}
