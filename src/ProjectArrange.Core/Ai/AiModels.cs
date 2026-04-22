namespace ProjectArrange.Core.Ai;

public sealed record AiChatRequest(
    string Prompt,
    string? System = null);

public sealed record AiChatResponse(
    string Text,
    string? Raw = null);

