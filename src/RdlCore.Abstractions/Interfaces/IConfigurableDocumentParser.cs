using RdlCore.Abstractions.Models;

namespace RdlCore.Abstractions.Interfaces;

public interface IConfigurableDocumentParser : IDocumentParser
{
    Task<DocumentStructureModel> ParseAsync(
        Stream stream,
        ConversionOptions options,
        CancellationToken cancellationToken = default);
}
