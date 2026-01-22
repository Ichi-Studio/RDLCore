namespace RdlCore.Rendering.Word;

internal sealed class WordReportService : IWordReportService
{
    public async Task<byte[]> GenerateEditableWordAsync(
        Stream sourceDocx,
        CancellationToken cancellationToken = default)
    {
        if (sourceDocx.CanSeek)
        {
            sourceDocx.Position = 0;
        }

        using var ms = new MemoryStream();
        await sourceDocx.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}

