using System.Data;
using System.Xml.Linq;

namespace RdlCore.WebApi.Controllers;

[ApiController]
[Route("api/render")]
public class RenderController : ControllerBase
{
    private readonly IValidationService _validationService;

    public RenderController(IValidationService validationService)
    {
        _validationService = validationService;
    }

    [HttpPost("pdf")]
    [Produces("application/pdf")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> RenderPdf(
        IFormFile rdlc,
        CancellationToken cancellationToken = default)
    {
        if (rdlc == null || rdlc.Length == 0)
        {
            return BadRequest("未提供 RDLC 文件。");
        }

        XDocument doc;
        await using (var stream = rdlc.OpenReadStream())
        {
            doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        }

        var pdf = await _validationService.RenderToPdfAsync(doc, new DataSet(), cancellationToken);
        return File(pdf, "application/pdf", "report.pdf");
    }

    [HttpPost("word")]
    [Produces("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> RenderWord(
        IFormFile rdlc,
        CancellationToken cancellationToken = default)
    {
        if (rdlc == null || rdlc.Length == 0)
        {
            return BadRequest("未提供 RDLC 文件。");
        }

        XDocument doc;
        await using (var stream = rdlc.OpenReadStream())
        {
            doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        }

        var word = await _validationService.RenderToWordOpenXmlAsync(doc, new DataSet(), cancellationToken);
        return File(word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "report.docx");
    }
}

