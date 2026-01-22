namespace RdlCore.Rendering.Word;

public interface IWordReportService
{
    Task<byte[]> GenerateEditableWordAsync(
        Stream sourceDocx,
        CancellationToken cancellationToken = default);
}

