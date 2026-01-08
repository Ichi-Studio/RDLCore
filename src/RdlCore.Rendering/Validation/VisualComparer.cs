namespace RdlCore.Rendering.Validation;

/// <summary>
/// Calculates Structural Similarity Index (SSIM) for visual comparison
/// </summary>
public class SsimCalculator
{
    private readonly ILogger<SsimCalculator> _logger;

    // SSIM constants
    private const double K1 = 0.01;
    private const double K2 = 0.03;
    private const int L = 255; // Dynamic range for 8-bit images

    private readonly double _c1 = Math.Pow(K1 * L, 2);
    private readonly double _c2 = Math.Pow(K2 * L, 2);

    public SsimCalculator(ILogger<SsimCalculator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates SSIM between two images
    /// </summary>
    /// <param name="image1">First image as byte array</param>
    /// <param name="image2">Second image as byte array</param>
    /// <returns>SSIM score between 0 and 1</returns>
    public double Calculate(byte[] image1, byte[] image2)
    {
        if (image1.Length != image2.Length)
        {
            _logger.LogWarning("Image sizes differ: {Size1} vs {Size2}", image1.Length, image2.Length);
            return 0;
        }

        if (image1.Length == 0)
        {
            return 1.0; // Empty images are considered identical
        }

        // Calculate means
        var mean1 = CalculateMean(image1);
        var mean2 = CalculateMean(image2);

        // Calculate variances and covariance
        var variance1 = CalculateVariance(image1, mean1);
        var variance2 = CalculateVariance(image2, mean2);
        var covariance = CalculateCovariance(image1, image2, mean1, mean2);

        // Calculate SSIM
        var numerator = (2 * mean1 * mean2 + _c1) * (2 * covariance + _c2);
        var denominator = (mean1 * mean1 + mean2 * mean2 + _c1) * (variance1 + variance2 + _c2);

        var ssim = numerator / denominator;

        _logger.LogDebug("SSIM calculated: {Ssim:F4}", ssim);
        return ssim;
    }

    /// <summary>
    /// Calculates SSIM with windowed approach for better accuracy
    /// </summary>
    public double CalculateWindowed(byte[] image1, byte[] image2, int width, int height, int windowSize = 8)
    {
        if (image1.Length != image2.Length)
        {
            return 0;
        }

        var windowCount = 0;
        var totalSsim = 0.0;

        for (int y = 0; y <= height - windowSize; y += windowSize)
        {
            for (int x = 0; x <= width - windowSize; x += windowSize)
            {
                var window1 = ExtractWindow(image1, width, x, y, windowSize);
                var window2 = ExtractWindow(image2, width, x, y, windowSize);

                totalSsim += Calculate(window1, window2);
                windowCount++;
            }
        }

        return windowCount > 0 ? totalSsim / windowCount : 0;
    }

    private double CalculateMean(byte[] data)
    {
        return data.Average(b => (double)b);
    }

    private double CalculateVariance(byte[] data, double mean)
    {
        return data.Average(b => Math.Pow(b - mean, 2));
    }

    private double CalculateCovariance(byte[] data1, byte[] data2, double mean1, double mean2)
    {
        var sum = 0.0;
        for (int i = 0; i < data1.Length; i++)
        {
            sum += (data1[i] - mean1) * (data2[i] - mean2);
        }
        return sum / data1.Length;
    }

    private byte[] ExtractWindow(byte[] image, int stride, int x, int y, int size)
    {
        var window = new byte[size * size];
        var idx = 0;

        for (int dy = 0; dy < size; dy++)
        {
            for (int dx = 0; dx < size; dx++)
            {
                var sourceIdx = (y + dy) * stride + (x + dx);
                if (sourceIdx < image.Length)
                {
                    window[idx++] = image[sourceIdx];
                }
            }
        }

        return window;
    }
}

/// <summary>
/// Compares visual output between documents
/// </summary>
public class VisualComparer
{
    private readonly ILogger<VisualComparer> _logger;
    private readonly SsimCalculator _ssimCalculator;
    private readonly AxiomRdlCoreOptions _options;

    public VisualComparer(
        ILogger<VisualComparer> logger,
        SsimCalculator ssimCalculator,
        Microsoft.Extensions.Options.IOptions<AxiomRdlCoreOptions> options)
    {
        _logger = logger;
        _ssimCalculator = ssimCalculator;
        _options = options.Value;
    }

    /// <summary>
    /// Compares two images and returns the comparison result
    /// </summary>
    public VisualComparisonResult Compare(byte[] sourceImage, byte[] renderedImage)
    {
        _logger.LogInformation("Comparing visual output: source={SourceSize}B, rendered={RenderedSize}B",
            sourceImage.Length, renderedImage.Length);

        var threshold = _options.Validation.VisualComparisonThreshold;
        var ssimScore = _ssimCalculator.Calculate(sourceImage, renderedImage);
        var isMatch = ssimScore >= threshold;

        _logger.LogInformation("Visual comparison result: SSIM={Ssim:F4}, Threshold={Threshold:F2}, Match={IsMatch}",
            ssimScore, threshold, isMatch);

        var differences = new List<DifferenceRegion>();

        if (!isMatch)
        {
            // Identify difference regions (simplified)
            differences.Add(new DifferenceRegion(
                Bounds: new BoundingBox(0, 0, 100, 100),
                Severity: 1.0 - ssimScore,
                Description: $"Visual difference detected (SSIM: {ssimScore:F4})"));
        }

        return new VisualComparisonResult(
            IsMatch: isMatch,
            SsimScore: ssimScore,
            Threshold: threshold,
            DifferenceImage: null,
            Differences: differences);
    }

    /// <summary>
    /// Generates a difference image highlighting changes
    /// </summary>
    public byte[]? GenerateDifferenceImage(byte[] image1, byte[] image2)
    {
        if (image1.Length != image2.Length)
        {
            return null;
        }

        var diff = new byte[image1.Length];
        
        for (int i = 0; i < image1.Length; i++)
        {
            diff[i] = (byte)Math.Abs(image1[i] - image2[i]);
        }

        return diff;
    }
}
