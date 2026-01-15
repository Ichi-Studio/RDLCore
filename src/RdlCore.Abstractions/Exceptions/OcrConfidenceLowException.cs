namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// 当 OCR 置信度太低时抛出的异常
/// </summary>
public class OcrConfidenceLowException(double confidence, double threshold, string? text = null)
    : RdlCoreException($"OCR confidence {confidence:P1} is below threshold {threshold:P1}")
{
    /// <summary>
    /// 置信度分数
    /// </summary>
    public double Confidence { get; } = confidence;

    /// <summary>
    /// 所需的最低置信度
    /// </summary>
    public double Threshold { get; } = threshold;

    /// <summary>
    /// 识别的文本
    /// </summary>
    public string? RecognizedText { get; } = text;
}
