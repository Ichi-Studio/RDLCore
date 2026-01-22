using RdlCore.Abstractions.Models;
using RdlCore.Parsing.Image;
using RdlCore.Parsing.Ocr;
using OpenCvSharp;

namespace RdlCore.Parsing.Tests;

public class ImageDocumentParserTests
{
    [Fact]
    public async Task ParseAsync_WhenOcrDisabled_ShouldReturnSingleImageElement()
    {
        var logger = Mock.Of<ILogger<ImageDocumentParser>>();
        var ocr = new Mock<IOcrService>(MockBehavior.Strict);
        var parser = new ImageDocumentParser(logger, ocr.Object);

        var options = new ConversionOptions(
            DataSetName: null,
            SchemaPath: null,
            StyleTemplate: null,
            ForceOverwrite: false,
            VerboseOutput: false,
            DryRun: false,
            OcrEnabled: false,
            OcrLanguage: "zh-Hans",
            OcrConfidenceThreshold: 0.8);

        using var mat = new Mat(10, 10, MatType.CV_8UC3, new Scalar(255, 255, 255));
        Cv2.ImEncode(".png", mat, out var buffer);
        using var stream = new MemoryStream(buffer);
        var result = await parser.ParseAsync(stream, options);

        result.Type.Should().Be(DocumentType.Image);
        result.Pages.Should().HaveCount(1);
        result.Pages[0].Elements.Should().HaveCount(1);
        result.Pages[0].Elements[0].Should().BeOfType<ImageElement>();
    }
}
