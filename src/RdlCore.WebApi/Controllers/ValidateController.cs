using System.Text;
using System.Xml.Linq;
using RdlCore.WebApi.Models;

namespace RdlCore.WebApi.Controllers;

/// <summary>
/// RDLC 验证 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ValidateController : ControllerBase
{
    private readonly IValidationService _validationService;
    private readonly ILogger<ValidateController> _logger;

    public ValidateController(
        IValidationService validationService,
        ILogger<ValidateController> logger)
    {
        _validationService = validationService;
        _logger = logger;
    }

    /// <summary>
    /// 验证 RDLC 文件的有效性
    /// </summary>
    /// <param name="file">要验证的 RDLC 文件</param>
    /// <param name="request">验证选项</param>
    /// <returns>验证结果</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ValidateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidateResponse), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<ActionResult<ValidateResponse>> Validate(
        IFormFile file,
        [FromQuery] ValidateRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new ValidateRequest();

        if (file == null || file.Length == 0)
        {
            return BadRequest(new ValidateResponse
            {
                IsValid = false,
                Message = "请上传一个有效的文件",
                Errors = [new ValidationError { ErrorType = "InvalidInput", Message = "未提供文件或文件为空" }]
            });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".rdlc" && extension != ".rdl" && extension != ".xml")
        {
            return BadRequest(new ValidateResponse
            {
                IsValid = false,
                Message = $"不支持的文件格式: {extension}",
                Errors = [new ValidationError { ErrorType = "InvalidFormat", Message = "仅支持 .rdlc, .rdl, .xml 格式" }]
            });
        }

        try
        {
            _logger.LogInformation("开始验证 RDLC 文件: {FileName}", file.FileName);

            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            var content = await reader.ReadToEndAsync(cancellationToken);

            XDocument rdlDocument;
            try
            {
                rdlDocument = XDocument.Parse(content);
            }
            catch (Exception ex)
            {
                return Ok(new ValidateResponse
                {
                    IsValid = false,
                    Message = "XML 解析失败",
                    Errors = [new ValidationError { ErrorType = "XmlParsing", Message = ex.Message }]
                });
            }

            var errors = new List<ValidationError>();
            var warnings = new List<string>();

            // Schema validation
            var schemaResult = await _validationService.ValidateSchemaAsync(rdlDocument, cancellationToken);
            var schemaInfo = new SchemaValidationInfo
            {
                IsValid = schemaResult.IsValid,
                ErrorCount = schemaResult.Errors.Count
            };

            foreach (var message in schemaResult.Errors)
            {
                errors.Add(new ValidationError
                {
                    ErrorType = "Schema",
                    Message = message.Message,
                    Location = message.Location,
                    SuggestedFix = GetSuggestedFix(message.Message)
                });
            }

            foreach (var message in schemaResult.Warnings)
            {
                warnings.Add(message.Message);
            }

            // Expression validation
            ExpressionValidationInfo? expressionInfo = null;
            if (request.ValidateExpressions)
            {
                var expressionResult = await _validationService.ValidateExpressionsAsync(rdlDocument, cancellationToken);
                var exprErrorCount = expressionResult.Messages.Count(m => m.Severity == ValidationSeverity.Error);
                var exprWarningCount = expressionResult.Messages.Count(m => m.Severity == ValidationSeverity.Warning);
                
                expressionInfo = new ExpressionValidationInfo
                {
                    AllValid = expressionResult.IsValid,
                    TotalExpressions = expressionResult.Messages.Count,
                    ValidExpressions = expressionResult.Messages.Count - exprErrorCount,
                    InvalidExpressions = exprErrorCount
                };

                foreach (var message in expressionResult.Messages.Where(m => m.Severity == ValidationSeverity.Error))
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "Expression",
                        Message = message.Message,
                        Location = message.Location
                    });
                }

                foreach (var message in expressionResult.Messages.Where(m => m.Severity == ValidationSeverity.Warning))
                {
                    warnings.Add(message.Message);
                }
            }

            var isValid = schemaResult.IsValid && (expressionInfo?.AllValid ?? true);

            return Ok(new ValidateResponse
            {
                IsValid = isValid,
                Message = isValid ? "验证通过" : "验证失败，请查看错误详情",
                SchemaValidation = schemaInfo,
                ExpressionValidation = expressionInfo,
                Errors = errors,
                Warnings = warnings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证 RDLC 时发生错误: {FileName}", file.FileName);
            return StatusCode(500, new ValidateResponse
            {
                IsValid = false,
                Message = "服务器内部错误",
                Errors = [new ValidationError { ErrorType = "Internal", Message = ex.Message }]
            });
        }
    }

    /// <summary>
    /// 验证 RDLC 内容（以文本形式提交）
    /// </summary>
    /// <param name="content">RDLC XML 内容</param>
    /// <param name="validateExpressions">是否验证表达式</param>
    /// <returns>验证结果</returns>
    [HttpPost("content")]
    [ProducesResponseType(typeof(ValidateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidateResponse), StatusCodes.Status400BadRequest)]
    [Consumes("text/xml", "application/xml", "text/plain")]
    public async Task<ActionResult<ValidateResponse>> ValidateContent(
        [FromBody] string content,
        [FromQuery] bool validateExpressions = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new ValidateResponse
            {
                IsValid = false,
                Message = "请提供 RDLC 内容",
                Errors = [new ValidationError { ErrorType = "InvalidInput", Message = "内容为空" }]
            });
        }

        try
        {
            XDocument rdlDocument;
            try
            {
                rdlDocument = XDocument.Parse(content);
            }
            catch (Exception ex)
            {
                return Ok(new ValidateResponse
                {
                    IsValid = false,
                    Message = "XML 解析失败",
                    Errors = [new ValidationError { ErrorType = "XmlParsing", Message = ex.Message }]
                });
            }

            var errors = new List<ValidationError>();

            var schemaResult = await _validationService.ValidateSchemaAsync(rdlDocument, cancellationToken);
            var schemaInfo = new SchemaValidationInfo
            {
                IsValid = schemaResult.IsValid,
                ErrorCount = schemaResult.Errors.Count
            };

            foreach (var message in schemaResult.Errors)
            {
                errors.Add(new ValidationError
                {
                    ErrorType = "Schema",
                    Message = message.Message
                });
            }

            ExpressionValidationInfo? expressionInfo = null;
            if (validateExpressions)
            {
                var expressionResult = await _validationService.ValidateExpressionsAsync(rdlDocument, cancellationToken);
                var exprErrorCount = expressionResult.Messages.Count(m => m.Severity == ValidationSeverity.Error);
                
                expressionInfo = new ExpressionValidationInfo
                {
                    AllValid = expressionResult.IsValid,
                    TotalExpressions = expressionResult.Messages.Count,
                    ValidExpressions = expressionResult.Messages.Count - exprErrorCount,
                    InvalidExpressions = exprErrorCount
                };

                foreach (var message in expressionResult.Messages.Where(m => m.Severity == ValidationSeverity.Error))
                {
                    errors.Add(new ValidationError
                    {
                        ErrorType = "Expression",
                        Message = message.Message,
                        Location = message.Location
                    });
                }
            }

            var isValid = schemaResult.IsValid && (expressionInfo?.AllValid ?? true);

            return Ok(new ValidateResponse
            {
                IsValid = isValid,
                Message = isValid ? "验证通过" : "验证失败",
                SchemaValidation = schemaInfo,
                ExpressionValidation = expressionInfo,
                Errors = errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证 RDLC 内容时发生错误");
            return StatusCode(500, new ValidateResponse
            {
                IsValid = false,
                Message = ex.Message,
                Errors = [new ValidationError { ErrorType = "Internal", Message = ex.Message }]
            });
        }
    }

    private static string? GetSuggestedFix(string error)
    {
        if (error.Contains("namespace", StringComparison.OrdinalIgnoreCase))
        {
            return "确保根元素使用正确的 RDL 2016 命名空间: http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";
        }
        if (error.Contains("required", StringComparison.OrdinalIgnoreCase))
        {
            return "添加缺失的必需元素";
        }
        if (error.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            return "检查元素值是否符合 RDL 规范";
        }
        return null;
    }
}
