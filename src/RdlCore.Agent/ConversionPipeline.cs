using System.Diagnostics;

namespace RdlCore.Agent;

/// <summary>
/// Executes the five-phase conversion pipeline
/// </summary>
public class ConversionPipeline : IConversionPipelineService
{
    private readonly ILogger<ConversionPipeline> _logger;
    private readonly IDocumentPerceptionService _perceptionService;
    private readonly ILogicDecompositionService _decompositionService;
    private readonly ISchemaSynthesisService _synthesisService;
    private readonly ILogicTranslationService _translationService;
    private readonly IValidationService _validationService;
    private readonly IHumanInterventionHandler _interventionHandler;
    private readonly ProgressReporter _progressReporter;

    public ConversionPipeline(
        ILogger<ConversionPipeline> logger,
        IDocumentPerceptionService perceptionService,
        ILogicDecompositionService decompositionService,
        ISchemaSynthesisService synthesisService,
        ILogicTranslationService translationService,
        IValidationService validationService,
        IHumanInterventionHandler interventionHandler,
        ProgressReporter progressReporter)
    {
        _logger = logger;
        _perceptionService = perceptionService;
        _decompositionService = decompositionService;
        _synthesisService = synthesisService;
        _translationService = translationService;
        _validationService = validationService;
        _interventionHandler = interventionHandler;
        _progressReporter = progressReporter;
    }

    /// <inheritdoc />
    public async Task<ConversionResult> ExecuteAsync(
        ConversionRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var completedPhases = new List<PhaseResult>();
        var messages = new List<ValidationMessage>();
        var interventions = new List<InterventionRequest>();

        _logger.LogInformation("Starting conversion pipeline");

        try
        {
            // Phase 1: Perception
            var perceptionResult = await ExecutePhaseAsync(
                PipelinePhase.Perception,
                progress,
                completedPhases,
                async ct => await _perceptionService.AnalyzeAsync(
                    request.DocumentStream,
                    request.DocumentType ?? DocumentType.Unknown,
                    ct),
                cancellationToken);

            var documentStructure = perceptionResult!;

            // Phase 2: Decomposition
            var decompositionResult = await ExecutePhaseAsync(
                PipelinePhase.Decomposition,
                progress,
                completedPhases,
                async ct => await _decompositionService.ExtractFieldCodesAsync(documentStructure, ct),
                cancellationToken);

            var logicResult = decompositionResult!;

            // Phase 3: Synthesis - Generate multiple documents if needed
            var synthesisResult = await ExecutePhaseAsync(
                PipelinePhase.Synthesis,
                progress,
                completedPhases,
                async ct => await _synthesisService.GenerateMultipleRdlDocumentsAsync(documentStructure, logicResult, ct),
                cancellationToken);

            var rdlDocuments = synthesisResult!;
            var primaryDocument = rdlDocuments.Count > 0 ? rdlDocuments[0].Document : null;
            var additionalDocs = rdlDocuments.Count > 1 
                ? rdlDocuments.Skip(1).ToList().AsReadOnly() 
                : null;

            // Phase 4: Translation
            await ExecutePhaseAsync(
                PipelinePhase.Translation,
                progress,
                completedPhases,
                async ct =>
                {
                    foreach (var condition in logicResult.Conditions)
                    {
                        var expression = await _translationService.TranslateToVbExpressionAsync(
                            condition.Condition, ct);
                        var optimized = await _translationService.OptimizeExpressionAsync(expression, ct);
                        _logger.LogDebug("Translated condition {Id}: {Expression}", condition.Id, optimized);
                    }
                    return true;
                },
                cancellationToken);

            // Phase 5: Validation - validate primary document
            var validationResult = await ExecutePhaseAsync(
                PipelinePhase.Validation,
                progress,
                completedPhases,
                async ct =>
                {
                    if (primaryDocument == null) return false;

                    var schemaResult = await _validationService.ValidateSchemaAsync(primaryDocument, ct);
                    messages.AddRange(schemaResult.Errors);
                    messages.AddRange(schemaResult.Warnings);

                    var expressionResult = await _validationService.ValidateExpressionsAsync(primaryDocument, ct);
                    messages.AddRange(expressionResult.Messages);

                    return schemaResult.IsValid && expressionResult.IsValid;
                },
                cancellationToken);

            var isValid = (bool)validationResult!;

            // Save output if path specified
            string? outputPath = null;
            if (!string.IsNullOrEmpty(request.OutputPath) && !request.Options.DryRun && primaryDocument != null)
            {
                outputPath = request.OutputPath;
                await SaveDocumentAsync(primaryDocument, outputPath);
                
                // Save additional documents with numbered suffixes
                if (additionalDocs != null)
                {
                    var basePath = Path.GetDirectoryName(outputPath) ?? ".";
                    var baseName = Path.GetFileNameWithoutExtension(outputPath);
                    var extension = Path.GetExtension(outputPath);
                    
                    foreach (var doc in additionalDocs)
                    {
                        var docPath = Path.Combine(basePath, $"{baseName}_{doc.PageNumber}{extension}");
                        await SaveDocumentAsync(doc.Document, docPath);
                    }
                }
            }

            stopwatch.Stop();

            var status = isValid
                ? (messages.Exists(m => m.Severity == ValidationSeverity.Warning)
                    ? ConversionStatus.CompletedWithWarnings
                    : ConversionStatus.Completed)
                : ConversionStatus.Failed;

            var summary = primaryDocument != null 
                ? CreateSummary(primaryDocument, messages) 
                : CreateEmptySummary();

            _logger.LogInformation("Conversion pipeline completed in {Elapsed}ms with status {Status}, generated {DocCount} documents",
                stopwatch.ElapsedMilliseconds, status, rdlDocuments.Count);

            return new ConversionResult(
                Status: status,
                RdlDocument: primaryDocument,
                OutputPath: outputPath,
                Summary: summary,
                Messages: messages.AsReadOnly(),
                InterventionRequests: interventions.AsReadOnly(),
                ElapsedTime: stopwatch.Elapsed,
                AdditionalDocuments: additionalDocs);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Conversion pipeline cancelled");
            stopwatch.Stop();

            return new ConversionResult(
                Status: ConversionStatus.Cancelled,
                RdlDocument: null,
                OutputPath: null,
                Summary: CreateEmptySummary(),
                Messages: messages.AsReadOnly(),
                InterventionRequests: interventions.AsReadOnly(),
                ElapsedTime: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversion pipeline failed");
            stopwatch.Stop();

            messages.Add(new ValidationMessage(
                ValidationSeverity.Error,
                "PIPELINE001",
                $"Pipeline execution failed: {ex.Message}",
                null));

            return new ConversionResult(
                Status: ConversionStatus.Failed,
                RdlDocument: null,
                OutputPath: null,
                Summary: CreateEmptySummary(),
                Messages: messages.AsReadOnly(),
                InterventionRequests: interventions.AsReadOnly(),
                ElapsedTime: stopwatch.Elapsed);
        }
    }

    private async Task<T?> ExecutePhaseAsync<T>(
        PipelinePhase phase,
        IProgress<PipelineProgress>? progress,
        List<PhaseResult> completedPhases,
        Func<CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken)
    {
        var phaseStopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("Starting phase: {Phase}", phase);
        _progressReporter.ReportPhaseStart(phase, progress, completedPhases);

        try
        {
            var result = await execute(cancellationToken);
            
            phaseStopwatch.Stop();
            completedPhases.Add(new PhaseResult(phase, true, phaseStopwatch.Elapsed, null));
            
            _logger.LogInformation("Phase {Phase} completed in {Elapsed}ms", 
                phase, phaseStopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            phaseStopwatch.Stop();
            completedPhases.Add(new PhaseResult(phase, false, phaseStopwatch.Elapsed, ex.Message));
            throw;
        }
    }

    private async Task SaveDocumentAsync(XDocument document, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        await document.SaveAsync(stream, SaveOptions.None, CancellationToken.None);
        
        _logger.LogInformation("Saved RDLC document to: {Path}", path);
    }

    private ConversionSummary CreateSummary(XDocument document, IReadOnlyList<ValidationMessage> messages)
    {
        var root = document.Root;
        
        var textboxCount = root?.Descendants()
            .Count(e => e.Name.LocalName == "Textbox") ?? 0;
        
        var tablixCount = root?.Descendants()
            .Count(e => e.Name.LocalName == "Tablix") ?? 0;
        
        var imageCount = root?.Descendants()
            .Count(e => e.Name.LocalName == "Image") ?? 0;
        
        var chartCount = root?.Descendants()
            .Count(e => e.Name.LocalName == "Chart") ?? 0;
        
        var expressionCount = root?.Descendants()
            .Count(e => e.Name.LocalName == "Value" && e.Value.StartsWith("=")) ?? 0;

        return new ConversionSummary(
            TextboxCount: textboxCount,
            TablixCount: tablixCount,
            ImageCount: imageCount,
            ChartCount: chartCount,
            ExpressionCount: expressionCount,
            WarningCount: messages.Count(m => m.Severity == ValidationSeverity.Warning),
            ErrorCount: messages.Count(m => m.Severity == ValidationSeverity.Error));
    }

    private ConversionSummary CreateEmptySummary()
    {
        return new ConversionSummary(0, 0, 0, 0, 0, 0, 0);
    }
}
