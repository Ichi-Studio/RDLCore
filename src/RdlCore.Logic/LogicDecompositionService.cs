using RdlCore.Logic.Extraction;

namespace RdlCore.Logic;

/// <summary>
/// Implementation of logic decomposition service
/// </summary>
public class LogicDecompositionService : ILogicDecompositionService
{
    private readonly ILogger<LogicDecompositionService> _logger;
    private readonly FieldCodeParser _fieldCodeParser;
    private readonly ConditionalAnalyzer _conditionalAnalyzer;
    private readonly AstBuilder _astBuilder;

    public LogicDecompositionService(
        ILogger<LogicDecompositionService> logger,
        FieldCodeParser fieldCodeParser,
        ConditionalAnalyzer conditionalAnalyzer,
        AstBuilder astBuilder)
    {
        _logger = logger;
        _fieldCodeParser = fieldCodeParser;
        _conditionalAnalyzer = conditionalAnalyzer;
        _astBuilder = astBuilder;
    }

    /// <inheritdoc />
    public async Task<LogicExtractionResult> ExtractFieldCodesAsync(
        DocumentStructureModel model,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting field codes from document");

        var fieldCodes = new List<FieldCode>();
        var warnings = new List<string>();

        // Extract field codes from paragraphs
        foreach (var page in model.Pages)
        {
            foreach (var element in page.Elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (element is ParagraphElement para)
                {
                    foreach (var run in para.Runs.Where(r => r.FieldCode != null))
                    {
                        fieldCodes.Add(run.FieldCode!);
                    }
                }
            }
        }

        // Analyze conditions
        var conditions = _conditionalAnalyzer.AnalyzeConditions(fieldCodes).ToList();

        // Extract formulas
        var formulas = ExtractFormulas(fieldCodes);

        _logger.LogInformation(
            "Extracted {FieldCount} field codes, {ConditionCount} conditions, {FormulaCount} formulas",
            fieldCodes.Count, conditions.Count, formulas.Count);

        return new LogicExtractionResult(
            FieldCodes: fieldCodes,
            Conditions: conditions,
            Formulas: formulas,
            Warnings: warnings);
    }

    /// <inheritdoc />
    public async Task<AbstractSyntaxTree> BuildAstAsync(
        string expression,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Building AST for expression: {Expression}", expression);
        return _astBuilder.Build(expression);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ConditionalBranch>> IdentifyConditionsAsync(
        LogicExtractionResult result,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Identifying conditions from {Count} field codes", result.FieldCodes.Count);

        var conditions = new List<ConditionalBranch>(result.Conditions);

        // Flatten nested conditions
        var flattened = result.Conditions
            .SelectMany(c => _conditionalAnalyzer.FlattenNestedConditions(c))
            .Distinct()
            .ToList();

        return flattened;
    }

    private IReadOnlyList<CalculationFormula> ExtractFormulas(IEnumerable<FieldCode> fieldCodes)
    {
        var formulas = new List<CalculationFormula>();
        var formulaId = 0;

        foreach (var fieldCode in fieldCodes.Where(f => f.Type == FieldCodeType.Formula))
        {
            var ast = _fieldCodeParser.Parse(fieldCode);
            
            formulas.Add(new CalculationFormula(
                Id: $"formula_{++formulaId}",
                RawExpression: fieldCode.RawCode,
                ParsedExpression: ast,
                SourceLocation: fieldCode.Id));
        }

        return formulas;
    }
}
