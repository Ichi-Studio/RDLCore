namespace RdlCore.Agent;

/// <summary>
/// 在转换过程中处理人工干预请求
/// </summary>
public class HumanInterventionHandler(
    ILogger<HumanInterventionHandler> logger,
    Func<InterventionRequest, Task<InterventionResponse>>? interactionCallback = null) : IHumanInterventionHandler
{

    /// <inheritdoc />
    public async Task<InterventionResponse> RequestInterventionAsync(
        InterventionRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Human intervention required: Type={Type}, Element={Element}, Confidence={Confidence}",
            request.Type, request.ElementPath, request.Confidence);

        LogInterventionDetails(request);

        if (interactionCallback != null)
        {
            return await interactionCallback(request);
        }

        // 默认行为：选择第一个选项或返回空响应
        var defaultOption = request.Options.FirstOrDefault();
        
        logger.LogInformation("Auto-selecting option: {Option}", 
            defaultOption?.Description ?? "None");

        return new InterventionResponse(
            RequestId: request.ElementPath,
            SelectedOptionId: defaultOption?.Id ?? string.Empty,
            CustomValue: null);
    }

    /// <summary>
    /// 为复杂逻辑创建干预请求
    /// </summary>
    public InterventionRequest CreateComplexLogicRequest(
        string elementPath,
        string sourceContent,
        IEnumerable<string> suggestedSolutions)
    {
        var options = suggestedSolutions
            .Select((s, i) => new InterventionOption($"opt_{i}", s, null))
            .ToList();

        options.Add(new InterventionOption("custom", "Custom handling", null));

        return new InterventionRequest(
            Type: InterventionType.ComplexLogicReview,
            ElementPath: elementPath,
            SourceContent: sourceContent,
            SuggestedAction: "Review complex logic and select appropriate handling",
            Confidence: ConfidenceLevel.Low,
            Options: options);
    }

    /// <summary>
    /// 为模糊布局创建干预请求
    /// </summary>
    public InterventionRequest CreateAmbiguousLayoutRequest(
        string elementPath,
        string description,
        IEnumerable<(string id, string description, string? preview)> layoutOptions)
    {
        var options = layoutOptions
            .Select(o => new InterventionOption(o.id, o.description, o.preview))
            .ToList();

        return new InterventionRequest(
            Type: InterventionType.AmbiguousLayout,
            ElementPath: elementPath,
            SourceContent: description,
            SuggestedAction: "Select the correct layout interpretation",
            Confidence: ConfidenceLevel.Medium,
            Options: options);
    }

    /// <summary>
    /// 为低 OCR 置信度创建干预请求
    /// </summary>
    public InterventionRequest CreateLowOcrConfidenceRequest(
        string elementPath,
        string recognizedText,
        double confidence)
    {
        return new InterventionRequest(
            Type: InterventionType.LowOcrConfidence,
            ElementPath: elementPath,
            SourceContent: recognizedText,
            SuggestedAction: $"Confirm or correct OCR result (confidence: {confidence:P1})",
            Confidence: confidence switch
            {
                < 0.5 => ConfidenceLevel.VeryLow,
                < 0.7 => ConfidenceLevel.Low,
                < 0.85 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.High
            },
            Options: new[]
            {
                new InterventionOption("accept", "Accept recognized text", recognizedText),
                new InterventionOption("edit", "Edit text manually", null),
                new InterventionOption("skip", "Skip this element", null)
            });
    }

    /// <summary>
    /// 为不支持的功能创建干预请求
    /// </summary>
    public InterventionRequest CreateUnsupportedFeatureRequest(
        string elementPath,
        string featureDescription,
        IEnumerable<string> alternatives)
    {
        var options = alternatives
            .Select((a, i) => new InterventionOption($"alt_{i}", a, null))
            .Append(new InterventionOption("skip", "Skip this feature", null))
            .ToList();

        return new InterventionRequest(
            Type: InterventionType.UnsupportedFeature,
            ElementPath: elementPath,
            SourceContent: featureDescription,
            SuggestedAction: "Feature not supported in RDL. Select an alternative approach.",
            Confidence: ConfidenceLevel.Medium,
            Options: options);
    }

    private void LogInterventionDetails(InterventionRequest request)
    {
        logger.LogDebug(@"
## Human Intervention Required

### Location
Element: {ElementPath}

### Issue Description
Type: {Type}
Content: {SourceContent}

### Suggested Action
{SuggestedAction}

### Available Options
{Options}",
            request.ElementPath,
            request.Type,
            request.SourceContent,
            request.SuggestedAction,
            string.Join("\n", request.Options.Select(o => $"- [{o.Id}] {o.Description}")));
    }
}
