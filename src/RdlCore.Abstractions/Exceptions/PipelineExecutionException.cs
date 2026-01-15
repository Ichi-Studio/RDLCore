namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// 当管道执行失败时抛出的异常
/// </summary>
public class PipelineExecutionException : RdlCoreException
{
    /// <summary>
    /// 发生失败的阶段
    /// </summary>
    public PipelinePhase Phase { get; }

    /// <summary>
    /// 创建新的 PipelineExecutionException
    /// </summary>
    public PipelineExecutionException(string message, PipelinePhase phase)
        : base(message)
    {
        Phase = phase;
    }

    /// <summary>
    /// 创建带有内部异常的 PipelineExecutionException
    /// </summary>
    public PipelineExecutionException(string message, PipelinePhase phase, Exception innerException)
        : base(message, innerException)
    {
        Phase = phase;
    }
}
