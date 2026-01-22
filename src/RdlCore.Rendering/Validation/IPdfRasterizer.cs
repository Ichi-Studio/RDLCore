namespace RdlCore.Rendering.Validation;

public interface IPdfRasterizer
{
    Task<IReadOnlyList<RasterizedPage>> RasterizeAsync(
        byte[] pdfBytes,
        int dpi,
        CancellationToken cancellationToken = default);
}

public record RasterizedPage(
    int PageNumber,
    int Width,
    int Height,
    byte[] Bgra32Pixels);

