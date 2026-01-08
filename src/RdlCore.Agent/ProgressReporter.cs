namespace RdlCore.Agent;

/// <summary>
/// Reports pipeline progress
/// </summary>
public class ProgressReporter
{
    private readonly ILogger<ProgressReporter> _logger;
    private const int TotalPhases = 5;

    public ProgressReporter(ILogger<ProgressReporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reports the start of a pipeline phase
    /// </summary>
    public void ReportPhaseStart(
        PipelinePhase phase,
        IProgress<PipelineProgress>? progress,
        IReadOnlyList<PhaseResult> completedPhases)
    {
        var phaseNumber = (int)phase;
        var percentComplete = (phaseNumber - 1) * 100.0 / TotalPhases;
        var message = GetPhaseMessage(phase);

        _logger.LogInformation("[{Phase}/{Total}] {Message}...", phaseNumber, TotalPhases, message);

        progress?.Report(new PipelineProgress(
            CurrentPhase: phase,
            TotalPhases: TotalPhases,
            PercentComplete: percentComplete,
            StatusMessage: message,
            CompletedPhases: completedPhases));
    }

    /// <summary>
    /// Reports the completion of a pipeline phase
    /// </summary>
    public void ReportPhaseComplete(
        PipelinePhase phase,
        IProgress<PipelineProgress>? progress,
        IReadOnlyList<PhaseResult> completedPhases,
        string? additionalInfo = null)
    {
        var phaseNumber = (int)phase;
        var percentComplete = phaseNumber * 100.0 / TotalPhases;
        var message = $"{GetPhaseMessage(phase)} complete";
        
        if (!string.IsNullOrEmpty(additionalInfo))
        {
            message += $" ({additionalInfo})";
        }

        _logger.LogInformation("[{Phase}/{Total}] {Message} ✓", phaseNumber, TotalPhases, message);

        progress?.Report(new PipelineProgress(
            CurrentPhase: phase,
            TotalPhases: TotalPhases,
            PercentComplete: percentComplete,
            StatusMessage: message,
            CompletedPhases: completedPhases));
    }

    /// <summary>
    /// Reports a pipeline error
    /// </summary>
    public void ReportError(
        PipelinePhase phase,
        IProgress<PipelineProgress>? progress,
        IReadOnlyList<PhaseResult> completedPhases,
        string errorMessage)
    {
        _logger.LogError("[{Phase}] Error: {Message}", phase, errorMessage);

        progress?.Report(new PipelineProgress(
            CurrentPhase: phase,
            TotalPhases: TotalPhases,
            PercentComplete: ((int)phase - 1) * 100.0 / TotalPhases,
            StatusMessage: $"Error: {errorMessage}",
            CompletedPhases: completedPhases));
    }

    private static string GetPhaseMessage(PipelinePhase phase) => phase switch
    {
        PipelinePhase.Perception => "Analyzing document structure",
        PipelinePhase.Decomposition => "Extracting logic and expressions",
        PipelinePhase.Synthesis => "Generating RDLC schema",
        PipelinePhase.Translation => "Translating expressions to VBScript",
        PipelinePhase.Validation => "Validating generated report",
        _ => "Processing"
    };
}
