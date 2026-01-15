namespace RdlCore.Logic.Translation;

/// <summary>
/// Validates expressions against RDL sandbox security rules
/// </summary>
public partial class SandboxValidator
{
    private readonly ILogger<SandboxValidator> _logger;

    // Allowed namespaces in RDL sandbox
    private static readonly HashSet<string> AllowedNamespaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Convert",
        "System.Math",
        "System.String",
        "System.DateTime",
        "System.TimeSpan",
        "Microsoft.VisualBasic.Strings",
        "Microsoft.VisualBasic.DateAndTime",
        "Microsoft.VisualBasic.Conversion",
        "Microsoft.VisualBasic.Financial",
        "Microsoft.VisualBasic.Information",
        "Microsoft.VisualBasic.Interaction"
    };

    // Allowed built-in functions
    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Conditional
        "IIf", "If", "Switch", "Choose",
        
        // Type checking
        "IsNothing", "IsNumeric", "IsDate", "IsArray",
        
        // Conversion
        "CStr", "CInt", "CLng", "CDbl", "CDec", "CBool", "CDate", "CByte", "CShort",
        "Val", "Str",
        
        // String functions
        "Len", "Left", "Right", "Mid", "Trim", "LTrim", "RTrim",
        "UCase", "LCase", "StrComp", "InStr", "InStrRev",
        "Replace", "Split", "Join", "Space", "String",
        "Asc", "Chr", "Format",
        
        // Math functions
        "Abs", "Int", "Fix", "Round", "Sgn",
        "Sqr", "Log", "Exp", "Sin", "Cos", "Tan", "Atn",
        "Rnd", "Randomize",
        
        // Date/Time functions
        "Now", "Today", "Year", "Month", "Day",
        "Hour", "Minute", "Second", "Weekday", "WeekdayName",
        "DateAdd", "DateDiff", "DatePart", "DateSerial", "DateValue",
        "TimeSerial", "TimeValue", "Timer",
        "MonthName", "FormatDateTime",
        
        // Aggregate functions
        "Sum", "Avg", "Count", "CountDistinct", "CountRows",
        "Max", "Min", "First", "Last", "Previous", "RunningValue",
        "RowNumber", "Aggregate", "StDev", "StDevP", "Var", "VarP",
        
        // Array functions
        "Array", "UBound", "LBound",
        
        // Utility
        "Format", "Lookup", "LookupSet", "MultiLookup"
    };

    // Prohibited patterns
    private static readonly string[] ProhibitedPatterns =
    {
        @"System\.IO\.",
        @"System\.Net\.",
        @"System\.Reflection\.",
        @"System\.Diagnostics\.",
        @"System\.Threading\.",
        @"System\.Security\.",
        @"Process\.",
        @"File\.",
        @"Directory\.",
        @"Assembly\.",
        @"AppDomain\.",
        @"Activator\.",
        @"Type\.GetType",
        @"Invoke\s*\(",
        @"CreateObject\s*\(",
        @"GetObject\s*\(",
        @"Shell\s*\(",
        @"Environ\s*\("
    };

    public SandboxValidator(ILogger<SandboxValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates an expression against sandbox rules
    /// </summary>
    public ExpressionValidationResult Validate(string expression)
    {
        var violations = new List<string>();
        var messages = new List<ValidationMessage>();

        // Check for prohibited patterns
        foreach (var pattern in ProhibitedPatterns)
        {
            if (Regex.IsMatch(expression, pattern, RegexOptions.IgnoreCase))
            {
                violations.Add($"Prohibited pattern detected: {pattern}");
                messages.Add(new ValidationMessage(
                    Abstractions.Enums.ValidationSeverity.Error,
                    "SANDBOX001",
                    $"Expression contains prohibited pattern: {pattern}",
                    null));
            }
        }

        // Check for unknown function calls
        var functionCalls = ExtractFunctionCalls(expression);
        foreach (var func in functionCalls)
        {
            if (!AllowedFunctions.Contains(func) && !IsFieldOrParameter(func))
            {
                messages.Add(new ValidationMessage(
                    Abstractions.Enums.ValidationSeverity.Warning,
                    "SANDBOX002",
                    $"Unknown function call: {func}. Ensure it's a valid RDL function.",
                    null));
            }
        }

        // Check for potential namespace access
        var namespaceAccess = ExtractNamespaceAccess(expression);
        foreach (var ns in namespaceAccess)
        {
            if (!AllowedNamespaces.Any(a => ns.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"Access to namespace not allowed: {ns}");
                messages.Add(new ValidationMessage(
                    Abstractions.Enums.ValidationSeverity.Error,
                    "SANDBOX003",
                    $"Access to namespace '{ns}' is not allowed in RDL sandbox",
                    null));
            }
        }

        var isValid = violations.Count == 0;
        
        if (!isValid)
        {
            _logger.LogWarning("Expression validation failed: {Expression}, Violations: {Violations}",
                expression, string.Join("; ", violations));
        }

        return new ExpressionValidationResult(isValid, messages, violations);
    }

    /// <summary>
    /// Validates an expression and throws if invalid
    /// </summary>
    public void ValidateOrThrow(string expression)
    {
        var result = Validate(expression);
        
        if (!result.IsValid)
        {
            throw new SandboxViolationException(expression, result.SandboxViolations);
        }
    }

    private IEnumerable<string> ExtractFunctionCalls(string expression)
    {
        var matches = FunctionCallRegex().Matches(expression);
        return matches.Select(m => m.Groups[1].Value).Distinct();
    }

    private IEnumerable<string> ExtractNamespaceAccess(string expression)
    {
        var matches = NamespaceRegex().Matches(expression);
        return matches
            .Select(m => m.Value)
            .Where(ns => !IsRdlBuiltInReference(ns) && !IsFieldPropertyAccess(ns)) // Filter out RDL built-in references and field property access
            .Distinct();
    }

    private bool IsRdlBuiltInReference(string reference)
    {
        // Check if it's an RDL built-in collection like Fields.Value, Parameters.Value, etc.
        return reference.StartsWith("Fields.", StringComparison.OrdinalIgnoreCase) ||
               reference.StartsWith("Parameters.", StringComparison.OrdinalIgnoreCase) ||
               reference.StartsWith("Globals.", StringComparison.OrdinalIgnoreCase) ||
               reference.StartsWith("User.", StringComparison.OrdinalIgnoreCase) ||
               reference.StartsWith("Code.", StringComparison.OrdinalIgnoreCase) ||
               reference.StartsWith("ReportItems.", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsFieldPropertyAccess(string reference)
    {
        // Check if it's a field property access like Name.Value, Date.Value, Amount.Value, etc.
        // These appear in expressions like =Fields!Name.Value
        // The pattern is: single word followed by .Value or .IsMissing
        var parts = reference.Split('.');
        if (parts.Length == 2)
        {
            var property = parts[1];
            return property.Equals("Value", StringComparison.OrdinalIgnoreCase) ||
                   property.Equals("IsMissing", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private bool IsFieldOrParameter(string name)
    {
        return name.StartsWith("Fields", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Parameters", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Globals", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("User", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Code", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(\w+)\s*\(")]
    private static partial Regex FunctionCallRegex();

    [GeneratedRegex(@"[A-Z][a-z]+(?:\.[A-Z][a-z]+)+")]
    private static partial Regex NamespaceRegex();
}
