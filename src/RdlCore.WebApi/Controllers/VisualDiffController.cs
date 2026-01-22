using RdlCore.Rendering.Validation;

namespace RdlCore.WebApi.Controllers;

[ApiController]
[Route("api/visual-diff")]
[Produces("application/json")]
public class VisualDiffController : ControllerBase
{
    private readonly IVisualDiffService _visualDiffService;

    public VisualDiffController(IVisualDiffService visualDiffService)
    {
        _visualDiffService = visualDiffService;
    }

    [HttpPost("pdf")]
    [ProducesResponseType(typeof(VisualDiffReport), StatusCodes.Status200OK)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<VisualDiffReport>> ComparePdf(
        IFormFile baseline,
        IFormFile candidate,
        [FromQuery] int dpi = 300,
        CancellationToken cancellationToken = default)
    {
        if (baseline == null || baseline.Length == 0 || candidate == null || candidate.Length == 0)
        {
            return BadRequest("请同时上传 baseline 与 candidate PDF。");
        }

        byte[] baselineBytes;
        byte[] candidateBytes;

        await using (var s = baseline.OpenReadStream())
        using (var ms = new MemoryStream())
        {
            await s.CopyToAsync(ms, cancellationToken);
            baselineBytes = ms.ToArray();
        }

        await using (var s = candidate.OpenReadStream())
        using (var ms = new MemoryStream())
        {
            await s.CopyToAsync(ms, cancellationToken);
            candidateBytes = ms.ToArray();
        }

        var report = await _visualDiffService.ComparePdfAsync(
            baselineBytes,
            candidateBytes,
            new VisualDiffOptions(Dpi: dpi, RequiredSsim: 1.0, MaxDifferentPixelRatio: 0.0, OutputDirectory: null, WriteDiffImages: false),
            cancellationToken);

        return Ok(report);
    }
}

