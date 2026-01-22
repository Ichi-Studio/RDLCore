using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RdlCore.Rendering.Word;

internal sealed class WordToPdfConverter : IWordToPdfConverter
{
    private readonly ILogger<WordToPdfConverter> _logger;

    public WordToPdfConverter(ILogger<WordToPdfConverter> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> ConvertDocxToPdfAsync(
        Stream docxStream,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "RdlCore", "word2pdf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var inputPath = Path.Combine(tempDir, "input.docx");
        var outputPath = Path.Combine(tempDir, "output.pdf");

        try
        {
            if (docxStream.CanSeek)
            {
                docxStream.Position = 0;
            }

            await using (var fs = File.Create(inputPath))
            {
                await docxStream.CopyToAsync(fs, cancellationToken);
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    ConvertViaWordCom(inputPath, outputPath);
                    return await File.ReadAllBytesAsync(outputPath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Word COM conversion failed, trying LibreOffice fallback");
                }
            }

            await ConvertViaLibreOfficeAsync(inputPath, tempDir, cancellationToken);

            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException("Word 转 PDF 失败：未生成 output.pdf。");
            }

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private void ConvertViaWordCom(string inputPath, string outputPath)
    {
        var wordType = Type.GetTypeFromProgID("Word.Application");
        if (wordType == null)
        {
            throw new InvalidOperationException("未检测到 Microsoft Word（Word.Application）。");
        }

        object? wordApp = null;
        object? document = null;

        try
        {
            wordApp = Activator.CreateInstance(wordType);
            wordType.InvokeMember("Visible", System.Reflection.BindingFlags.SetProperty, null, wordApp, [false]);

            var documents = wordType.InvokeMember("Documents", System.Reflection.BindingFlags.GetProperty, null, wordApp, null);
            var documentsType = documents!.GetType();

            document = documentsType.InvokeMember(
                "Open",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                documents,
                [
                    inputPath,
                    false,
                    true,
                    false
                ]);

            var docType = document!.GetType();

            const int wdExportFormatPDF = 17;
            const int wdExportOptimizeForPrint = 0;
            const int wdExportAllDocument = 0;

            docType.InvokeMember(
                "ExportAsFixedFormat",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                document,
                [
                    outputPath,
                    wdExportFormatPDF,
                    false,
                    wdExportOptimizeForPrint,
                    wdExportAllDocument
                ]);
        }
        finally
        {
            if (document != null)
            {
                try
                {
                    document.GetType().InvokeMember("Close", System.Reflection.BindingFlags.InvokeMethod, null, document, [false]);
                }
                catch
                {
                }
                try
                {
                    Marshal.FinalReleaseComObject(document);
                }
                catch
                {
                }
            }

            if (wordApp != null)
            {
                try
                {
                    wordApp.GetType().InvokeMember("Quit", System.Reflection.BindingFlags.InvokeMethod, null, wordApp, null);
                }
                catch
                {
                }
                try
                {
                    Marshal.FinalReleaseComObject(wordApp);
                }
                catch
                {
                }
            }
        }
    }

    private async Task ConvertViaLibreOfficeAsync(string inputPath, string outputDir, CancellationToken cancellationToken)
    {
        var sofficePath = FindSofficePath();
        if (string.IsNullOrWhiteSpace(sofficePath))
        {
            throw new InvalidOperationException("无法将 Word 转为 PDF：未检测到 Word COM，也未找到 LibreOffice soffice 可执行文件。");
        }

        var psi = new ProcessStartInfo
        {
            FileName = sofficePath,
            Arguments = $"--headless --nologo --nodefault --nofirststartwizard --nolockcheck --convert-to pdf --outdir \"{outputDir}\" \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("启动 LibreOffice 失败。");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stdOut = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErr = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"LibreOffice 转换失败 (ExitCode={process.ExitCode}). stdout={stdOut} stderr={stdErr}");
        }

        var producedPdf = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + ".pdf");
        var targetPdf = Path.Combine(outputDir, "output.pdf");
        if (File.Exists(producedPdf) && !File.Exists(targetPdf))
        {
            File.Move(producedPdf, targetPdf);
        }
    }

    private static string? FindSofficePath()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("SOFFICE_PATH"),
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        };

        foreach (var path in candidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
