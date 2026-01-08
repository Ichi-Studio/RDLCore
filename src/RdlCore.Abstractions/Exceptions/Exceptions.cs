using RdlCore.Abstractions.Enums;
using RdlCore.Abstractions.Models;

namespace RdlCore.Abstractions.Exceptions;

/// <summary>
/// Base exception for RDL Core operations
/// </summary>
public class RdlCoreException(string message, Exception? innerException = null) 
    : Exception(message, innerException)
{
}

/// <summary>
/// Exception thrown when schema validation fails
/// </summary>
public class SchemaValidationException(IReadOnlyList<ValidationMessage> errors)
    : RdlCoreException($"Schema validation failed with {errors.Count} error(s)")
{
    /// <summary>
    /// The validation errors
    /// </summary>
    public IReadOnlyList<ValidationMessage> ValidationErrors { get; } = errors;
}

/// <summary>
/// Exception thrown when expression syntax is invalid
/// </summary>
public class ExpressionSyntaxException(string message, string expression, int? position = null)
    : RdlCoreException(message)
{
    /// <summary>
    /// The invalid expression
    /// </summary>
    public string Expression { get; } = expression;

    /// <summary>
    /// The position of the error
    /// </summary>
    public int? Position { get; } = position;
}

/// <summary>
/// Exception thrown when a field code is not supported
/// </summary>
public class UnsupportedFieldCodeException(FieldCodeType type, string rawCode)
    : RdlCoreException($"Unsupported field code type: {type}")
{
    /// <summary>
    /// The field code type
    /// </summary>
    public FieldCodeType FieldCodeType { get; } = type;

    /// <summary>
    /// The raw field code
    /// </summary>
    public string RawFieldCode { get; } = rawCode;
}

/// <summary>
/// Exception thrown when layout recognition fails
/// </summary>
public class LayoutRecognitionException(string message, int? pageNumber = null)
    : RdlCoreException(message)
{
    /// <summary>
    /// The page number where the error occurred
    /// </summary>
    public int? PageNumber { get; } = pageNumber;
}

/// <summary>
/// Exception thrown when OCR confidence is too low
/// </summary>
public class OcrConfidenceLowException(double confidence, double threshold, string? text = null)
    : RdlCoreException($"OCR confidence {confidence:P1} is below threshold {threshold:P1}")
{
    /// <summary>
    /// The confidence score
    /// </summary>
    public double Confidence { get; } = confidence;

    /// <summary>
    /// The minimum required confidence
    /// </summary>
    public double Threshold { get; } = threshold;

    /// <summary>
    /// The recognized text
    /// </summary>
    public string? RecognizedText { get; } = text;
}

/// <summary>
/// Exception thrown when document parsing fails
/// </summary>
public class DocumentParsingException : RdlCoreException
{
    /// <summary>
    /// The document type
    /// </summary>
    public DocumentType DocumentType { get; }

    /// <summary>
    /// Creates a new DocumentParsingException
    /// </summary>
    public DocumentParsingException(string message, DocumentType type)
        : base(message)
    {
        DocumentType = type;
    }

    /// <summary>
    /// Creates a new DocumentParsingException with inner exception
    /// </summary>
    public DocumentParsingException(string message, DocumentType type, Exception innerException)
        : base(message, innerException)
    {
        DocumentType = type;
    }
}

/// <summary>
/// Exception thrown when pipeline execution fails
/// </summary>
public class PipelineExecutionException : RdlCoreException
{
    /// <summary>
    /// The phase where the failure occurred
    /// </summary>
    public PipelinePhase Phase { get; }

    /// <summary>
    /// Creates a new PipelineExecutionException
    /// </summary>
    public PipelineExecutionException(string message, PipelinePhase phase)
        : base(message)
    {
        Phase = phase;
    }

    /// <summary>
    /// Creates a new PipelineExecutionException with inner exception
    /// </summary>
    public PipelineExecutionException(string message, PipelinePhase phase, Exception innerException)
        : base(message, innerException)
    {
        Phase = phase;
    }
}

/// <summary>
/// Exception thrown when sandbox security is violated
/// </summary>
public class SandboxViolationException(string expression, IReadOnlyList<string> violatedRules)
    : RdlCoreException($"Expression violates sandbox security rules: {string.Join(", ", violatedRules)}")
{
    /// <summary>
    /// The expression that violated security
    /// </summary>
    public string Expression { get; } = expression;

    /// <summary>
    /// The violated rules
    /// </summary>
    public IReadOnlyList<string> ViolatedRules { get; } = violatedRules;
}
