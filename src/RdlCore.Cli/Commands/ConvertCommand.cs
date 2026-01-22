using System.Data;
using System.Xml.Linq;
using RdlCore.Generation.Schema;

namespace RdlCore.Cli.Commands;

/// <summary>
/// Convert command - converts documents to RDLC
/// </summary>
public class ConvertCommand : Command
{
    private readonly Argument<FileInfo> _inputArg;
    private readonly Option<string?> _outputOption;
    private readonly Option<string?> _datasetOption;
    private readonly Option<bool> _forceOption;
    private readonly Option<bool> _verboseOption;
    private readonly Option<bool> _dryRunOption;
    private readonly Option<bool> _ocrOption;
    private readonly Option<string?> _ocrLanguageOption;
    private readonly Option<double> _ocrConfidenceOption;
    private readonly Option<bool> _emitWordOption;
    private readonly Option<bool> _emitPdfOption;
    private readonly Option<bool> _verifyVisualOption;
    private readonly Option<int> _dpiOption;
    private readonly Option<string?> _artifactsDirOption;
    private readonly Option<bool> _strictFidelityOption;

    public ConvertCommand() : base("convert", "Convert a document to RDLC format")
    {
        _inputArg = new Argument<FileInfo>("input", "Input document file (.docx, .pdf, or image files like .png, .jpg, .bmp, .tiff)");
        _outputOption = new Option<string?>(["--output", "-o"], "Output path for the generated RDLC file");
        _datasetOption = new Option<string?>(["--dataset", "-d"], "DataSet name to use in the report");
        _forceOption = new Option<bool>(["--force", "-f"], "Overwrite existing output file");
        _verboseOption = new Option<bool>(["--verbose", "-v"], "Enable verbose output");
        _dryRunOption = new Option<bool>("--dry-run", "Simulate conversion without saving");
        
        // OCR options for image input
        _ocrOption = new Option<bool>("--ocr", () => true, "Enable OCR for image-based input (default: true)");
        _ocrLanguageOption = new Option<string?>(["--ocr-lang", "-l"], () => "en", "OCR language code (e.g., 'en', 'zh-Hans', 'ja')");
        _ocrConfidenceOption = new Option<double>("--ocr-confidence", () => 0.8, "Minimum OCR confidence threshold (0.0-1.0)");

        _emitWordOption = new Option<bool>("--emit-word", "Also output an editable Word report (.docx)");
        _emitPdfOption = new Option<bool>("--emit-pdf", "Also output a PDF report for verification");
        _verifyVisualOption = new Option<bool>("--verify-visual", "Generate pixel-level visual diff report (Word->PDF baseline vs RDLC->PDF)");
        _dpiOption = new Option<int>("--dpi", () => 300, "Rasterization DPI used for pixel-level verification (default: 300)");
        _artifactsDirOption = new Option<string?>("--artifacts-dir", "Directory for extra outputs (docx/pdf/diff report). Defaults to output directory.");
        _strictFidelityOption = new Option<bool>("--strict-fidelity", "Enable strict fidelity mode (disable heuristic adjustments in generation)");

        AddArgument(_inputArg);
        AddOption(_outputOption);
        AddOption(_datasetOption);
        AddOption(_forceOption);
        AddOption(_verboseOption);
        AddOption(_dryRunOption);
        AddOption(_ocrOption);
        AddOption(_ocrLanguageOption);
        AddOption(_ocrConfidenceOption);
        AddOption(_emitWordOption);
        AddOption(_emitPdfOption);
        AddOption(_verifyVisualOption);
        AddOption(_dpiOption);
        AddOption(_artifactsDirOption);
        AddOption(_strictFidelityOption);

        this.SetHandler(ExecuteAsync);
    }

    private async Task ExecuteAsync(InvocationContext context)
    {
        var input = context.ParseResult.GetValueForArgument(_inputArg);
        var output = context.ParseResult.GetValueForOption(_outputOption);
        var dataset = context.ParseResult.GetValueForOption(_datasetOption);
        var force = context.ParseResult.GetValueForOption(_forceOption);
        var verbose = context.ParseResult.GetValueForOption(_verboseOption);
        var dryRun = context.ParseResult.GetValueForOption(_dryRunOption);
        var ocrEnabled = context.ParseResult.GetValueForOption(_ocrOption);
        var ocrLanguage = context.ParseResult.GetValueForOption(_ocrLanguageOption);
        var ocrConfidence = context.ParseResult.GetValueForOption(_ocrConfidenceOption);
        var emitWord = context.ParseResult.GetValueForOption(_emitWordOption);
        var emitPdf = context.ParseResult.GetValueForOption(_emitPdfOption);
        var verifyVisual = context.ParseResult.GetValueForOption(_verifyVisualOption);
        var dpi = context.ParseResult.GetValueForOption(_dpiOption);
        var artifactsDir = context.ParseResult.GetValueForOption(_artifactsDirOption);
        var strictFidelity = context.ParseResult.GetValueForOption(_strictFidelityOption);

        var services = Program.CreateServices(verbose, strictFidelity);
        var pipeline = services.GetRequiredService<IConversionPipelineService>();
        var logger = services.GetRequiredService<ILogger<ConvertCommand>>();

        Console.WriteLine($"RDL-Core Converter v1.0.0");
        Console.WriteLine();

        if (!input.Exists)
        {
            Console.Error.WriteLine($"Error: Input file not found: {input.FullName}");
            Environment.ExitCode = 1;
            return;
        }

        // Check if input is an image file that requires OCR
        var extension = input.Extension.ToLowerInvariant();
        var isImageInput = extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff" or ".tif";
        
        if (isImageInput)
        {
            Console.WriteLine($"Image input detected: {input.Name}");
            Console.WriteLine($"OCR Enabled: {ocrEnabled}");
            if (ocrEnabled)
            {
                Console.WriteLine($"OCR Language: {ocrLanguage ?? "en"}");
                Console.WriteLine($"OCR Confidence Threshold: {ocrConfidence:P0}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: OCR is disabled. Image content will not be extracted.");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        // Determine output path
        var outputPath = output ?? Path.ChangeExtension(input.FullName, ".rdlc");
        
        if (File.Exists(outputPath) && !force && !dryRun)
        {
            Console.Error.WriteLine($"Error: Output file already exists: {outputPath}");
            Console.Error.WriteLine("Use --force to overwrite.");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine($"Converting: {input.Name}");
        Console.WriteLine($"Output: {(dryRun ? "(dry run)" : outputPath)}");
        Console.WriteLine();

        // Progress reporting
        var progress = new Progress<PipelineProgress>(p =>
        {
            var bar = new string('█', (int)(p.PercentComplete / 5));
            var empty = new string('░', 20 - bar.Length);
            Console.Write($"\r[{bar}{empty}] {p.PercentComplete:F0}% - {p.StatusMessage}".PadRight(80));
        });

        try
        {
            await using var fileStream = input.OpenRead();

            var request = new ConversionRequest(
                DocumentStream: fileStream,
                DocumentType: null, // Auto-detect
                OutputPath: dryRun ? null : outputPath,
                Options: new ConversionOptions(
                    DataSetName: dataset,
                    SchemaPath: null,
                    StyleTemplate: null,
                    ForceOverwrite: force,
                    VerboseOutput: verbose,
                    DryRun: dryRun,
                    OcrEnabled: ocrEnabled,
                    OcrLanguage: ocrLanguage,
                    OcrConfidenceThreshold: ocrConfidence));

            var result = await pipeline.ExecuteAsync(request, progress);

            Console.WriteLine();
            Console.WriteLine();

            PrintResult(result);

            if (!dryRun && (emitWord || emitPdf || verifyVisual))
            {
                await EmitArtifactsAsync(
                    services,
                    input,
                    outputPath,
                    force,
                    emitWord,
                    emitPdf,
                    verifyVisual,
                    dpi,
                    artifactsDir,
                    context.GetCancellationToken());
            }

            Environment.ExitCode = result.Status switch
            {
                ConversionStatus.Completed => 0,
                ConversionStatus.CompletedWithWarnings => 0,
                _ => 1
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.Error.WriteLine($"Error: {ex.Message}");
            
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }

            Environment.ExitCode = 1;
        }
    }

    private static async Task EmitArtifactsAsync(
        IServiceProvider services,
        FileInfo input,
        string outputRdlcPath,
        bool force,
        bool emitWord,
        bool emitPdf,
        bool verifyVisual,
        int dpi,
        string? artifactsDir,
        CancellationToken cancellationToken)
    {
        var ext = input.Extension.ToLowerInvariant();
        if (ext != ".docx")
        {
            return;
        }

        var outDir = !string.IsNullOrWhiteSpace(artifactsDir)
            ? artifactsDir
            : Path.GetDirectoryName(outputRdlcPath) ?? Path.GetDirectoryName(input.FullName) ?? ".";

        Directory.CreateDirectory(outDir);

        var baseName = Path.GetFileNameWithoutExtension(outputRdlcPath);
        var baselinePdfPath = Path.Combine(outDir, $"{baseName}.baseline.pdf");
        var rdlcDocxPath = Path.Combine(outDir, $"{baseName}.rdlc.docx");
        var rdlcPdfPath = Path.Combine(outDir, $"{baseName}.rdlc.pdf");
        var diffDir = Path.Combine(outDir, $"{baseName}.visual-diff");

        var wordToPdf = services.GetRequiredService<RdlCore.Rendering.Word.IWordToPdfConverter>();
        var visualDiff = services.GetRequiredService<RdlCore.Rendering.Validation.IVisualDiffService>();
        var validationService = services.GetRequiredService<IValidationService>();

        var originalDocxBytes = await File.ReadAllBytesAsync(input.FullName, cancellationToken);

        var rdlcDocument = XDocument.Load(outputRdlcPath);
        var dataSet = BuildDataSetFromRdl(rdlcDocument);

        if (emitPdf || verifyVisual)
        {
            byte[] baselinePdfBytes;
            await using (var originalStream = new MemoryStream(originalDocxBytes, writable: false))
            {
                baselinePdfBytes = await wordToPdf.ConvertDocxToPdfAsync(originalStream, cancellationToken);
            }

            if (force || !File.Exists(baselinePdfPath))
            {
                await File.WriteAllBytesAsync(baselinePdfPath, baselinePdfBytes, cancellationToken);
            }

            var rdlcPdfBytes = await validationService.RenderToPdfAsync(rdlcDocument, dataSet, cancellationToken);

            if (force || !File.Exists(rdlcPdfPath))
            {
                await File.WriteAllBytesAsync(rdlcPdfPath, rdlcPdfBytes, cancellationToken);
            }

            if (verifyVisual)
            {
                var report = await visualDiff.ComparePdfAsync(
                    baselinePdfBytes,
                    rdlcPdfBytes,
                    new RdlCore.Rendering.Validation.VisualDiffOptions(
                        Dpi: dpi,
                        RequiredSsim: 1.0,
                        MaxDifferentPixelRatio: 0.0,
                        OutputDirectory: diffDir,
                        WriteDiffImages: true),
                    cancellationToken);

                if (!report.IsMatch)
                {
                    Environment.ExitCode = 1;
                }
            }
        }

        if (emitWord)
        {
            var rdlcDocxBytes = await validationService.RenderToWordOpenXmlAsync(rdlcDocument, dataSet, cancellationToken);
            if (force || !File.Exists(rdlcDocxPath))
            {
                await File.WriteAllBytesAsync(rdlcDocxPath, rdlcDocxBytes, cancellationToken);
            }
        }
    }

    private static DataSet BuildDataSetFromRdl(XDocument rdlDocument)
    {
        var dataSet = new DataSet();

        var dataSets = rdlDocument.Root?
            .Elements(RdlNamespaces.Rdl + "DataSets")
            .Elements(RdlNamespaces.Rdl + "DataSet")
            .ToList();

        if (dataSets == null || dataSets.Count == 0)
        {
            return dataSet;
        }

        foreach (var dsElement in dataSets)
        {
            var name = dsElement.Attribute("Name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var table = new DataTable(name);

            var fields = dsElement
                .Element(RdlNamespaces.Rdl + "Fields")?
                .Elements(RdlNamespaces.Rdl + "Field")
                .ToList() ?? [];

            foreach (var field in fields)
            {
                var fieldName = field.Attribute("Name")?.Value;
                if (string.IsNullOrWhiteSpace(fieldName))
                {
                    continue;
                }

                if (!table.Columns.Contains(fieldName))
                {
                    table.Columns.Add(new DataColumn(fieldName, typeof(string)));
                }
            }

            if (table.Columns.Count > 0)
            {
                var row = table.NewRow();
                foreach (DataColumn col in table.Columns)
                {
                    row[col] = DBNull.Value;
                }
                table.Rows.Add(row);
            }

            dataSet.Tables.Add(table);
        }

        return dataSet;
    }

    private void PrintResult(ConversionResult result)
    {
        var statusIcon = result.Status switch
        {
            ConversionStatus.Completed => "✓",
            ConversionStatus.CompletedWithWarnings => "⚠",
            _ => "✗"
        };

        Console.WriteLine($"{statusIcon} Conversion {result.Status}");
        Console.WriteLine();

        if (result.OutputPath != null)
        {
            Console.WriteLine($"Output: {result.OutputPath}");
        }

        Console.WriteLine($"Duration: {result.ElapsedTime.TotalMilliseconds:F0}ms");
        Console.WriteLine();

        Console.WriteLine("Summary:");
        Console.WriteLine($"  Textboxes: {result.Summary.TextboxCount}");
        Console.WriteLine($"  Tables:    {result.Summary.TablixCount}");
        Console.WriteLine($"  Images:    {result.Summary.ImageCount}");
        Console.WriteLine($"  Expressions: {result.Summary.ExpressionCount}");

        if (result.Summary.WarningCount > 0 || result.Summary.ErrorCount > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Warnings: {result.Summary.WarningCount}");
            Console.WriteLine($"  Errors:   {result.Summary.ErrorCount}");
        }

        if (result.Messages.Any())
        {
            Console.WriteLine();
            Console.WriteLine("Messages:");
            foreach (var msg in result.Messages.Take(10))
            {
                var icon = msg.Severity switch
                {
                    ValidationSeverity.Error => "✗",
                    ValidationSeverity.Warning => "⚠",
                    _ => "ℹ"
                };
                Console.WriteLine($"  {icon} [{msg.Code}] {msg.Message}");
            }

            if (result.Messages.Count > 10)
            {
                Console.WriteLine($"  ... and {result.Messages.Count - 10} more");
            }
        }
    }
}
