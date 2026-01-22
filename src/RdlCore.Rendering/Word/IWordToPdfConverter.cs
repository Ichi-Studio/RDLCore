namespace RdlCore.Rendering.Word;

public interface IWordToPdfConverter
{
    Task<byte[]> ConvertDocxToPdfAsync(
        Stream docxStream,
        CancellationToken cancellationToken = default);
}

