using RdlCore.Abstractions.Models;

namespace RdlCore.Parsing.Ocr;

public interface IOcrService
{
    Task<IReadOnlyList<OcrRegion>> RecognizeAsync(
        byte[] imageBytes,
        string? language,
        double confidenceThreshold,
        CancellationToken cancellationToken = default);
}

public record OcrRegion(
    string Text,
    double Confidence,
    BoundingBox BoundsPixels);
