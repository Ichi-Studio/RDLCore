namespace RdlCore.Agent;

/// <summary>
/// 报告管道进度
/// </summary>
public class ProgressReporter(ILogger<ProgressReporter> logger)
{
    private const int TotalPhases = 5;

    /// <summary>
    /// 报告管道阶段开始
    /// </summary>
    public void ReportPhaseStart(
        PipelinePhase phase,
        IProgress<PipelineProgress>? progress,
        IReadOnlyList<PhaseResult> completedPhases)
    {
        var phaseNumber = (int)phase;
        var percentComplete = (phaseNumber - 1) * 100.0 / TotalPhases;
        var message = GetPhaseMessage(phase);

        logger.LogInformation("[{Phase}/{Total}] {Message}...", phaseNumber, TotalPhases, message);

        progress?.Report(new PipelineProgress(
            CurrentPhase: phase,
            TotalPhases: TotalPhases,
            PercentComplete: percentComplete,
            StatusMessage: message,
            CompletedPhases: completedPhases));
    }

    /// <summary>
    /// 报告管道阶段完成
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

        logger.LogInformation("[{Phase}/{Total}] {Message} ✓", phaseNumber, TotalPhases, message);

        progress?.Report(new PipelineProgress(
            CurrentPhase: phase,
            TotalPhases: TotalPhases,
            PercentComplete: percentComplete,
            StatusMessage: message,
            CompletedPhases: completedPhases));
    }

    /// <summary>
    /// 报告管道错误
    /// </summary>
    public void ReportError(
        PipelinePhase phase,
        IProgress<PipelineProgress>? progress,
        IReadOnlyList<PhaseResult> completedPhases,
        string errorMessage)
    {
        logger.LogError("[{Phase}] Error: {Message}", phase, errorMessage);

        progress?.Report(new PipelineProgress(
            CurrentPhase: phase,
            TotalPhases: TotalPhases,
            PercentComplete: ((int)phase - 1) * 100.0 / TotalPhases,
            StatusMessage: $"Error: {errorMessage}",
            CompletedPhases: completedPhases));
    }

    private static string GetPhaseMessage(PipelinePhase phase) => phase switch
    {
        PipelinePhase.Perception => "分析文档结构",
        PipelinePhase.Decomposition => "提取逻辑和表达式",
        PipelinePhase.Synthesis => "生成 RDLC 模式",
        PipelinePhase.Translation => "将表达式翻译为 VBScript",
        PipelinePhase.Validation => "验证生成的报表",
        _ => "处理中"
    };
}
