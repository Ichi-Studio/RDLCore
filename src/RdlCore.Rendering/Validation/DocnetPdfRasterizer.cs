using Docnet.Core;
using Docnet.Core.Models;

namespace RdlCore.Rendering.Validation;

internal sealed class DocnetPdfRasterizer : IPdfRasterizer
{
    private readonly ILogger<DocnetPdfRasterizer> _logger;

    public DocnetPdfRasterizer(ILogger<DocnetPdfRasterizer> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<RasterizedPage>> RasterizeAsync(
        byte[] pdfBytes,
        int dpi,
        CancellationToken cancellationToken = default)
    {
        var scale = dpi / 72.0;
        var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(scale));

        var pageCount = docReader.GetPageCount();
        var pages = new List<RasterizedPage>(pageCount);

        for (var i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageReader = docReader.GetPageReader(i);
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            var pixels = pageReader.GetImage();

            _logger.LogDebug("Rasterized PDF page {Page}/{Total}: {Width}x{Height} px", i + 1, pageCount, width, height);

            pages.Add(new RasterizedPage(
                PageNumber: i + 1,
                Width: width,
                Height: height,
                Bgra32Pixels: pixels));
        }

        IReadOnlyList<RasterizedPage> readOnlyPages = pages.AsReadOnly();
        return Task.FromResult(readOnlyPages);
    }
}
