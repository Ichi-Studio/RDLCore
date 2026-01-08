using RdlCore.Logic.Extraction;
using RdlCore.Logic.Translation;

namespace RdlCore.Logic;

/// <summary>
/// Implementation of logic translation service
/// </summary>
public class LogicTranslationService : ILogicTranslationService
{
    private readonly ILogger<LogicTranslationService> _logger;
    private readonly VbExpressionGenerator _expressionGenerator;
    private readonly ExpressionOptimizer _optimizer;
    private readonly SandboxValidator _sandboxValidator;

    public LogicTranslationService(
        ILogger<LogicTranslationService> logger,
        VbExpressionGenerator expressionGenerator,
        ExpressionOptimizer optimizer,
        SandboxValidator sandboxValidator)
    {
        _logger = logger;
        _expressionGenerator = expressionGenerator;
        _optimizer = optimizer;
        _sandboxValidator = sandboxValidator;
    }

    /// <inheritdoc />
    public async Task<string> TranslateToVbExpressionAsync(
        AbstractSyntaxTree ast,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Translating AST to VB expression");

        var expression = _expressionGenerator.Generate(ast);
        
        // Validate the generated expression
        var validation = _sandboxValidator.Validate(expression);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Generated expression has sandbox violations: {Expression}, Violations: {Violations}",
                expression, string.Join("; ", validation.SandboxViolations));
        }

        return await Task.FromResult(expression);
    }

    /// <inheritdoc />
    public async Task<ExpressionValidationResult> ValidateExpressionAsync(
        string expression,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Validating expression: {Expression}", expression);
        return await Task.FromResult(_sandboxValidator.Validate(expression));
    }

    /// <inheritdoc />
    public async Task<string> OptimizeExpressionAsync(
        string expression,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Optimizing expression: {Expression}", expression);
        return await Task.FromResult(_optimizer.Optimize(expression));
    }
}
