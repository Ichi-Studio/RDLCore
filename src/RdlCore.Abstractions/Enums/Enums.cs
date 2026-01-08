namespace RdlCore.Abstractions.Enums;

/// <summary>
/// Represents the type of source document
/// </summary>
public enum DocumentType
{
    /// <summary>Unknown document type</summary>
    Unknown = 0,
    
    /// <summary>Microsoft Word document (.docx)</summary>
    Word = 1,
    
    /// <summary>PDF document (.pdf)</summary>
    Pdf = 2,
    
    /// <summary>Rich Text Format (.rtf)</summary>
    Rtf = 3,
    
    /// <summary>Image file (.png, .jpg, .jpeg, .bmp, .tiff) - requires OCR</summary>
    Image = 4
}

/// <summary>
/// Represents the logical role of an element in a document
/// </summary>
public enum LogicalRole
{
    /// <summary>Unknown role</summary>
    Unknown = 0,
    
    /// <summary>Document header</summary>
    Header = 1,
    
    /// <summary>Document footer</summary>
    Footer = 2,
    
    /// <summary>Document title</summary>
    Title = 3,
    
    /// <summary>Body content</summary>
    Body = 4,
    
    /// <summary>Table element</summary>
    Table = 5,
    
    /// <summary>Paragraph text</summary>
    Paragraph = 6,
    
    /// <summary>Image element</summary>
    Image = 7,
    
    /// <summary>Chart element</summary>
    Chart = 8,
    
    /// <summary>Page number placeholder</summary>
    PageNumber = 9,
    
    /// <summary>Total pages placeholder</summary>
    TotalPages = 10,
    
    /// <summary>Date/time placeholder</summary>
    DateTime = 11
}

/// <summary>
/// Represents the type of field code in Word documents
/// </summary>
public enum FieldCodeType
{
    /// <summary>Unknown field type</summary>
    Unknown = 0,
    
    /// <summary>MERGEFIELD - Data binding field</summary>
    MergeField = 1,
    
    /// <summary>IF - Conditional field</summary>
    If = 2,
    
    /// <summary>DATE - Date field</summary>
    Date = 3,
    
    /// <summary>PAGE - Current page number</summary>
    Page = 4,
    
    /// <summary>NUMPAGES - Total pages</summary>
    NumPages = 5,
    
    /// <summary>TIME - Current time</summary>
    Time = 6,
    
    /// <summary>FORMULA - Calculation formula</summary>
    Formula = 7,
    
    /// <summary>SEQ - Sequence number</summary>
    Sequence = 8,
    
    /// <summary>TOC - Table of contents</summary>
    TableOfContents = 9,
    
    /// <summary>HYPERLINK - Hyperlink field</summary>
    Hyperlink = 10
}

/// <summary>
/// Represents operator types in expressions
/// </summary>
public enum OperatorType
{
    /// <summary>Equals (=)</summary>
    Equals = 0,
    
    /// <summary>Not equals (not equal)</summary>
    NotEquals = 1,
    
    /// <summary>Greater than</summary>
    GreaterThan = 2,
    
    /// <summary>Less than</summary>
    LessThan = 3,
    
    /// <summary>Greater than or equals</summary>
    GreaterThanOrEquals = 4,
    
    /// <summary>Less than or equals</summary>
    LessThanOrEquals = 5,
    
    /// <summary>Logical AND</summary>
    And = 6,
    
    /// <summary>Logical OR</summary>
    Or = 7,
    
    /// <summary>Logical NOT</summary>
    Not = 8,
    
    /// <summary>Addition (+)</summary>
    Add = 9,
    
    /// <summary>Subtraction (-)</summary>
    Subtract = 10,
    
    /// <summary>Multiplication (*)</summary>
    Multiply = 11,
    
    /// <summary>Division (/)</summary>
    Divide = 12,
    
    /// <summary>Modulo (%)</summary>
    Modulo = 13
}

/// <summary>
/// Represents the type of intervention required
/// </summary>
public enum InterventionType
{
    /// <summary>Complex logic requires manual review</summary>
    ComplexLogicReview = 0,
    
    /// <summary>Layout recognition ambiguity</summary>
    AmbiguousLayout = 1,
    
    /// <summary>Low OCR confidence</summary>
    LowOcrConfidence = 2,
    
    /// <summary>Unsupported source document feature</summary>
    UnsupportedFeature = 3,
    
    /// <summary>Expression validation failed</summary>
    ExpressionValidation = 4,
    
    /// <summary>Visual comparison difference too large</summary>
    VisualMismatch = 5
}

/// <summary>
/// Represents confidence levels
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>Very low confidence</summary>
    VeryLow = 0,
    
    /// <summary>Low confidence</summary>
    Low = 1,
    
    /// <summary>Medium confidence</summary>
    Medium = 2,
    
    /// <summary>High confidence</summary>
    High = 3,
    
    /// <summary>Very high confidence</summary>
    VeryHigh = 4
}

/// <summary>
/// Represents the status of a conversion process
/// </summary>
public enum ConversionStatus
{
    /// <summary>Not started</summary>
    NotStarted = 0,
    
    /// <summary>In progress</summary>
    InProgress = 1,
    
    /// <summary>Completed successfully</summary>
    Completed = 2,
    
    /// <summary>Completed with warnings</summary>
    CompletedWithWarnings = 3,
    
    /// <summary>Failed</summary>
    Failed = 4,
    
    /// <summary>Cancelled</summary>
    Cancelled = 5,
    
    /// <summary>Requires intervention</summary>
    RequiresIntervention = 6
}

/// <summary>
/// Represents the pipeline phase
/// </summary>
public enum PipelinePhase
{
    /// <summary>Perception phase - Document analysis</summary>
    Perception = 1,
    
    /// <summary>Decomposition phase - Logic extraction</summary>
    Decomposition = 2,
    
    /// <summary>Synthesis phase - Schema generation</summary>
    Synthesis = 3,
    
    /// <summary>Translation phase - Expression conversion</summary>
    Translation = 4,
    
    /// <summary>Validation phase - Final verification</summary>
    Validation = 5
}

/// <summary>
/// Represents validation severity levels
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Informational message</summary>
    Info = 0,
    
    /// <summary>Warning - may affect output</summary>
    Warning = 1,
    
    /// <summary>Error - critical issue</summary>
    Error = 2
}

/// <summary>
/// Represents aggregate function types
/// </summary>
public enum AggregateType
{
    /// <summary>Sum of values</summary>
    Sum = 0,
    
    /// <summary>Average of values</summary>
    Average = 1,
    
    /// <summary>Count of items</summary>
    Count = 2,
    
    /// <summary>Minimum value</summary>
    Min = 3,
    
    /// <summary>Maximum value</summary>
    Max = 4,
    
    /// <summary>First value</summary>
    First = 5,
    
    /// <summary>Last value</summary>
    Last = 6
}

/// <summary>
/// Represents AST node types
/// </summary>
public enum AstNodeType
{
    /// <summary>Literal value</summary>
    Literal = 0,
    
    /// <summary>Field reference</summary>
    FieldReference = 1,
    
    /// <summary>Parameter reference</summary>
    ParameterReference = 2,
    
    /// <summary>Global variable reference</summary>
    GlobalReference = 3,
    
    /// <summary>Binary operation</summary>
    BinaryOperation = 4,
    
    /// <summary>Unary operation</summary>
    UnaryOperation = 5,
    
    /// <summary>Function call</summary>
    FunctionCall = 6,
    
    /// <summary>Conditional expression</summary>
    Conditional = 7,
    
    /// <summary>Aggregate function</summary>
    Aggregate = 8
}
