using ProjectArrange.Core.Ai;

namespace ProjectArrange.Core.Abstractions;

public interface IAiService
{
    Task<Result<AiChatResponse>> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}

