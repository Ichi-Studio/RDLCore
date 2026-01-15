namespace RdlCore.Parsing.Tests;

public class DocumentTypeDetectorTests
{
    private readonly DocumentTypeDetector _detector;

    public DocumentTypeDetectorTests()
    {
        var logger = Mock.Of<ILogger<DocumentTypeDetector>>();
        _detector = new DocumentTypeDetector(logger);
    }

    [Theory]
    [InlineData(".docx", DocumentType.Word)]
    [InlineData(".doc", DocumentType.Word)]
    [InlineData(".pdf", DocumentType.Pdf)]
    [InlineData(".rtf", DocumentType.Rtf)]
    [InlineData(".txt", DocumentType.Unknown)]
    [InlineData("", DocumentType.Unknown)]
    public void DetectFromExtension_ShouldReturnCorrectType(string extension, DocumentType expected)
    {
        // Arrange
        var fileName = $"test{extension}";

        // Act
        var result = _detector.DetectFromExtension(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task DetectTypeAsync_WithPdfMagicBytes_ShouldReturnPdf()
    {
        // Arrange
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await _detector.DetectTypeAsync(stream);

        // Assert
        result.Should().Be(DocumentType.Pdf);
    }

    [Fact]
    public async Task DetectTypeAsync_WithEmptyStream_ShouldReturnUnknown()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _detector.DetectTypeAsync(stream);

        // Assert
        result.Should().Be(DocumentType.Unknown);
    }

    [Fact]
    public async Task DetectTypeAsync_ShouldResetStreamPosition()
    {
        // Arrange
        var bytes = new byte[100];
        using var stream = new MemoryStream(bytes);
        stream.Position = 50;

        // Act
        await _detector.DetectTypeAsync(stream);

        // Assert
        stream.Position.Should().Be(50);
    }
}

public class BoundingBoxCalculatorTests
{
    [Fact]
    public void EmuToPoints_ShouldConvertCorrectly()
    {
        // 914400 EMU = 1 inch = 72 points
        var result = BoundingBoxCalculator.EmuToPoints(914400);
        result.Should().BeApproximately(72, 0.001);
    }

    [Fact]
    public void TwipsToPoints_ShouldConvertCorrectly()
    {
        // 20 twips = 1 point
        var result = BoundingBoxCalculator.TwipsToPoints(20);
        result.Should().Be(1);
    }

    [Fact]
    public void PointsToInches_ShouldConvertCorrectly()
    {
        var result = BoundingBoxCalculator.PointsToInches(72);
        result.Should().Be(1);
    }

    [Fact]
    public void InchesToPoints_ShouldConvertCorrectly()
    {
        var result = BoundingBoxCalculator.InchesToPoints(1);
        result.Should().Be(72);
    }
}
