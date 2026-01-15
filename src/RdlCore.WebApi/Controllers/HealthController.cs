namespace RdlCore.WebApi.Controllers;

/// <summary>
/// 健康检查 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController(IServiceProvider serviceProvider) : ControllerBase
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    /// <summary>
    /// 获取服务健康状态
    /// </summary>
    /// <returns>健康状态信息</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetHealth()
    {
        var services = new Dictionary<string, bool>
        {
            ["DocumentPerceptionService"] = CheckService<IDocumentPerceptionService>(),
            ["LogicDecompositionService"] = CheckService<ILogicDecompositionService>(),
            ["SchemaSynthesisService"] = CheckService<ISchemaSynthesisService>(),
            ["LogicTranslationService"] = CheckService<ILogicTranslationService>(),
            ["ValidationService"] = CheckService<IValidationService>(),
            ["ConversionPipelineService"] = CheckService<IConversionPipelineService>()
        };

        var allHealthy = services.Values.All(v => v);

        return Ok(new HealthResponse
        {
            Status = allHealthy ? "healthy" : "degraded",
            Version = "1.0.0",
            Uptime = DateTime.UtcNow - StartTime,
            Services = services
        });
    }

    /// <summary>
    /// 简单存活检查
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult LiveCheck()
    {
        return Ok(new { status = "alive" });
    }

    /// <summary>
    /// 就绪检查
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult ReadyCheck()
    {
        var pipelineAvailable = CheckService<IConversionPipelineService>();
        
        if (pipelineAvailable)
        {
            return Ok(new { status = "ready" });
        }
        
        return StatusCode(503, new { status = "not ready", reason = "Core services unavailable" });
    }

    private bool CheckService<T>() where T : class
    {
        try
        {
            var service = serviceProvider.GetService<T>();
            return service != null;
        }
        catch
        {
            return false;
        }
    }
}
