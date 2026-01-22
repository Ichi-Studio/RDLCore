using OpenCvSharp;
using RdlCore.Abstractions.Models;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;

namespace RdlCore.Parsing.Ocr;

public sealed class PaddleOcrService : IOcrService, IDisposable
{
    private readonly ILogger<PaddleOcrService> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, PaddleOcrAll> _engines = new(StringComparer.OrdinalIgnoreCase);

    public PaddleOcrService(ILogger<PaddleOcrService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<OcrRegion>> RecognizeAsync(
        byte[] imageBytes,
        string? language,
        double confidenceThreshold,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (src.Empty())
        {
            return Task.FromResult<IReadOnlyList<OcrRegion>>([]);
        }

        var engine = GetOrCreateEngine(language);
        var result = engine.Run(src);

        var regions = new List<OcrRegion>();
        foreach (var r in result.Regions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (r.Score < confidenceThreshold)
            {
                continue;
            }

            var bounds = ToAxisAlignedBounds(r.Rect);
            regions.Add(new OcrRegion(
                Text: r.Text,
                Confidence: r.Score,
                BoundsPixels: bounds));
        }

        return Task.FromResult<IReadOnlyList<OcrRegion>>(regions.AsReadOnly());
    }

    private PaddleOcrAll GetOrCreateEngine(string? language)
    {
        var key = NormalizeLanguageKey(language);
        lock (_gate)
        {
            if (_engines.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var model = SelectModel(key);
            var engine = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = true,
                Enable180Classification = false
            };

            _engines[key] = engine;
            _logger.LogInformation("Initialized PaddleOCR engine: Language={Language}, Model={Model}", key, model.GetType().Name);
            return engine;
        }
    }

    private static string NormalizeLanguageKey(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "zh";
        }

        return language.Trim();
    }

    private static FullOcrModel SelectModel(string language)
    {
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return LocalFullModels.ChineseV3;
        }

        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return LocalFullModels.EnglishV3;
        }

        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return LocalFullModels.JapanV3;
        }

        if (language.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            return LocalFullModels.KoreanV3;
        }

        return LocalFullModels.EnglishV3;
    }

    private static BoundingBox ToAxisAlignedBounds(RotatedRect rect)
    {
        var left = rect.Center.X - rect.Size.Width / 2.0;
        var top = rect.Center.Y - rect.Size.Height / 2.0;
        return new BoundingBox(
            Left: left,
            Top: top,
            Width: rect.Size.Width,
            Height: rect.Size.Height);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var engine in _engines.Values)
            {
                engine.Dispose();
            }
            _engines.Clear();
        }
    }
}
