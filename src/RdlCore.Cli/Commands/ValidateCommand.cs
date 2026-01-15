namespace RdlCore.Cli.Commands;

/// <summary>
/// Validate command - validates existing RDLC files
/// </summary>
public class ValidateCommand : Command
{
    public ValidateCommand() : base("validate", "Validate an existing RDLC file")
    {
        var inputArg = new Argument<FileInfo>("input", "RDLC file to validate");
        var verboseOption = new Option<bool>(["--verbose", "-v"], "Show detailed validation output");

        AddArgument(inputArg);
        AddOption(verboseOption);

        this.SetHandler(ExecuteAsync, inputArg, verboseOption);
    }

    private async Task ExecuteAsync(FileInfo input, bool verbose)
    {
        var services = Program.CreateServices(verbose);
        var validationService = services.GetRequiredService<IValidationService>();
        var logger = services.GetRequiredService<ILogger<ValidateCommand>>();

        Console.WriteLine($"Axiom RDL-Core Validator v1.0.0");
        Console.WriteLine();

        if (!input.Exists)
        {
            Console.Error.WriteLine($"Error: File not found: {input.FullName}");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine($"Validating: {input.Name}");
        Console.WriteLine();

        try
        {
            var doc = XDocument.Load(input.FullName);

            // Schema validation
            Console.WriteLine("Schema validation...");
            var schemaResult = await validationService.ValidateSchemaAsync(doc);
            
            PrintValidationResult("Schema", schemaResult.IsValid, 
                schemaResult.Errors.Count, schemaResult.Warnings.Count);

            if (verbose)
            {
                foreach (var error in schemaResult.Errors)
                {
                    Console.WriteLine($"  ✗ [{error.Code}] {error.Message}");
                }
                foreach (var warning in schemaResult.Warnings)
                {
                    Console.WriteLine($"  ⚠ [{warning.Code}] {warning.Message}");
                }
            }

            // Expression validation
            Console.WriteLine("Expression validation...");
            var expressionResult = await validationService.ValidateExpressionsAsync(doc);
            
            var exprErrors = expressionResult.Messages.Count(m => m.Severity == ValidationSeverity.Error);
            var exprWarnings = expressionResult.Messages.Count(m => m.Severity == ValidationSeverity.Warning);
            
            PrintValidationResult("Expressions", expressionResult.IsValid, exprErrors, exprWarnings);

            if (verbose && expressionResult.SandboxViolations.Any())
            {
                Console.WriteLine("  Sandbox violations:");
                foreach (var violation in expressionResult.SandboxViolations)
                {
                    Console.WriteLine($"    ✗ {violation}");
                }
            }

            Console.WriteLine();

            var isValid = schemaResult.IsValid && expressionResult.IsValid;
            var icon = isValid ? "✓" : "✗";
            Console.WriteLine($"{icon} Validation {(isValid ? "passed" : "failed")}");

            Environment.ExitCode = isValid ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }

            Environment.ExitCode = 1;
        }
    }

    private void PrintValidationResult(string name, bool isValid, int errors, int warnings)
    {
        var icon = isValid ? "✓" : "✗";
        var status = isValid 
            ? (warnings > 0 ? $"passed ({warnings} warnings)" : "passed")
            : $"failed ({errors} errors, {warnings} warnings)";
        
        Console.WriteLine($"  {icon} {name}: {status}");
    }
}
