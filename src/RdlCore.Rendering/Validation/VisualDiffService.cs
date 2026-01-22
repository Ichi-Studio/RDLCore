using System.Text.Json;
using SkiaSharp;

namespace RdlCore.Rendering.Validation;

public record VisualDiffOptions(
    int Dpi = 300,
    double RequiredSsim = 1.0,
    double MaxDifferentPixelRatio = 0.0,
    string? OutputDirectory = null,
    bool WriteDiffImages = true);

public record VisualDiffReport(
    DateTimeOffset GeneratedAt,
    int Dpi,
    bool IsMatch,
    IReadOnlyList<VisualDiffPageReport> Pages);

public record VisualDiffPageReport(
    int PageNumber,
    int Width,
    int Height,
    double Ssim,
    int DifferentPixels,
    double DifferentPixelRatio,
    string? DiffImageFile);

public interface IVisualDiffService
{
    Task<VisualDiffReport> ComparePdfAsync(
        byte[] baselinePdf,
        byte[] candidatePdf,
        VisualDiffOptions options,
        CancellationToken cancellationToken = default);
}

internal sealed class VisualDiffService : IVisualDiffService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<VisualDiffService> _logger;
    private readonly IPdfRasterizer _pdfRasterizer;
    private readonly SsimCalculator _ssimCalculator;

    public VisualDiffService(
        ILogger<VisualDiffService> logger,
        IPdfRasterizer pdfRasterizer,
        SsimCalculator ssimCalculator)
    {
        _logger = logger;
        _pdfRasterizer = pdfRasterizer;
        _ssimCalculator = ssimCalculator;
    }

    public async Task<VisualDiffReport> ComparePdfAsync(
        byte[] baselinePdf,
        byte[] candidatePdf,
        VisualDiffOptions options,
        CancellationToken cancellationToken = default)
    {
        var baselinePages = await _pdfRasterizer.RasterizeAsync(baselinePdf, options.Dpi, cancellationToken);
        var candidatePages = await _pdfRasterizer.RasterizeAsync(candidatePdf, options.Dpi, cancellationToken);

        var pageCount = Math.Min(baselinePages.Count, candidatePages.Count);
        var results = new List<VisualDiffPageReport>(pageCount);

        var outputDir = options.OutputDirectory;
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            if (options.WriteDiffImages)
            {
                Directory.CreateDirectory(Path.Combine(outputDir, "diff-images"));
            }
        }

        var allMatch = baselinePages.Count == candidatePages.Count;

        for (var i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var b = baselinePages[i];
            var c = candidatePages[i];

            if (b.Width != c.Width || b.Height != c.Height)
            {
                var commonWidth = Math.Min(b.Width, c.Width);
                var commonHeight = Math.Min(b.Height, c.Height);

                if (commonWidth <= 0 || commonHeight <= 0)
                {
                    results.Add(new VisualDiffPageReport(
                        PageNumber: i + 1,
                        Width: Math.Max(b.Width, c.Width),
                        Height: Math.Max(b.Height, c.Height),
                        Ssim: 0,
                        DifferentPixels: int.MaxValue,
                        DifferentPixelRatio: 1.0,
                        DiffImageFile: null));
                    allMatch = false;
                    continue;
                }

                var croppedBaseline = CropBgra32(b.Width, b.Bgra32Pixels, commonWidth, commonHeight);
                var croppedCandidate = CropBgra32(c.Width, c.Bgra32Pixels, commonWidth, commonHeight);

                var grayBaseline = ToGrayscale(croppedBaseline);
                var grayCandidate = ToGrayscale(croppedCandidate);
                var ssimValue = _ssimCalculator.Calculate(grayBaseline, grayCandidate);

                var (diffPixelsValue, diffRatioValue, diffImageBytesValue) = CalculateDiff(commonWidth, commonHeight, croppedBaseline, croppedCandidate, options.WriteDiffImages);

                string? diffFileValue = null;
                if (!string.IsNullOrWhiteSpace(outputDir) && diffImageBytesValue != null)
                {
                    diffFileValue = Path.Combine("diff-images", $"diff-page-{i + 1:D3}.png");
                    var absPath = Path.Combine(outputDir, diffFileValue);
                    await File.WriteAllBytesAsync(absPath, diffImageBytesValue, cancellationToken);
                }

                results.Add(new VisualDiffPageReport(
                    PageNumber: i + 1,
                    Width: commonWidth,
                    Height: commonHeight,
                    Ssim: ssimValue,
                    DifferentPixels: diffPixelsValue,
                    DifferentPixelRatio: diffRatioValue,
                    DiffImageFile: diffFileValue));

                allMatch = false;
                continue;
            }

            var grayB = ToGrayscale(b.Bgra32Pixels);
            var grayC = ToGrayscale(c.Bgra32Pixels);
            var ssim = _ssimCalculator.Calculate(grayB, grayC);

            var (differentPixels, diffRatio, diffImageBytes) = CalculateDiff(b.Width, b.Height, b.Bgra32Pixels, c.Bgra32Pixels, options.WriteDiffImages);

            string? diffFile = null;
            if (!string.IsNullOrWhiteSpace(outputDir) && diffImageBytes != null)
            {
                diffFile = Path.Combine("diff-images", $"diff-page-{i + 1:D3}.png");
                var absPath = Path.Combine(outputDir, diffFile);
                await File.WriteAllBytesAsync(absPath, diffImageBytes, cancellationToken);
            }

            var pageMatch = ssim >= options.RequiredSsim && diffRatio <= options.MaxDifferentPixelRatio;
            allMatch &= pageMatch;

            results.Add(new VisualDiffPageReport(
                PageNumber: i + 1,
                Width: b.Width,
                Height: b.Height,
                Ssim: ssim,
                DifferentPixels: differentPixels,
                DifferentPixelRatio: diffRatio,
                DiffImageFile: diffFile));

            _logger.LogInformation(
                "Visual diff page {Page}: SSIM={Ssim:F6}, DiffPixels={DiffPixels}, DiffRatio={DiffRatio:P6}",
                i + 1,
                ssim,
                differentPixels,
                diffRatio);
        }

        if (baselinePages.Count != candidatePages.Count)
        {
            allMatch = false;
        }

        var report = new VisualDiffReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            Dpi: options.Dpi,
            IsMatch: allMatch,
            Pages: results.AsReadOnly());

        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            var json = JsonSerializer.Serialize(report, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(outputDir, "visual-diff-report.json"), json, cancellationToken);
        }

        return report;
    }

    private static byte[] ToGrayscale(byte[] bgra)
    {
        var gray = new byte[bgra.Length / 4];
        var gi = 0;
        for (var i = 0; i < bgra.Length; i += 4)
        {
            var b = bgra[i];
            var g = bgra[i + 1];
            var r = bgra[i + 2];
            gray[gi++] = (byte)Math.Clamp((0.114 * b) + (0.587 * g) + (0.299 * r), 0, 255);
        }
        return gray;
    }

    private static (int differentPixels, double diffRatio, byte[]? diffPng) CalculateDiff(
        int width,
        int height,
        byte[] baselineBgra,
        byte[] candidateBgra,
        bool generateDiffImage)
    {
        var pixelCount = width * height;
        var different = 0;

        byte[]? diffBgra = generateDiffImage ? new byte[baselineBgra.Length] : null;

        for (var i = 0; i < baselineBgra.Length; i += 4)
        {
            var mismatch =
                baselineBgra[i] != candidateBgra[i] ||
                baselineBgra[i + 1] != candidateBgra[i + 1] ||
                baselineBgra[i + 2] != candidateBgra[i + 2] ||
                baselineBgra[i + 3] != candidateBgra[i + 3];

            if (mismatch)
            {
                different++;
                if (diffBgra != null)
                {
                    diffBgra[i] = 0;
                    diffBgra[i + 1] = 0;
                    diffBgra[i + 2] = 255;
                    diffBgra[i + 3] = 255;
                }
            }
            else if (diffBgra != null)
            {
                diffBgra[i] = 0;
                diffBgra[i + 1] = 0;
                diffBgra[i + 2] = 0;
                diffBgra[i + 3] = 0;
            }
        }

        var ratio = pixelCount == 0 ? 0 : (double)different / pixelCount;

        if (diffBgra == null)
        {
            return (different, ratio, null);
        }

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        System.Runtime.InteropServices.Marshal.Copy(diffBgra, 0, bitmap.GetPixels(), diffBgra.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return (different, ratio, data.ToArray());
    }

    private static byte[] CropBgra32(int sourceWidth, byte[] sourceBgra, int targetWidth, int targetHeight)
    {
        var target = new byte[targetWidth * targetHeight * 4];
        var srcStride = sourceWidth * 4;
        var dstStride = targetWidth * 4;

        for (var row = 0; row < targetHeight; row++)
        {
            Buffer.BlockCopy(sourceBgra, row * srcStride, target, row * dstStride, dstStride);
        }

        return target;
    }
}
