namespace SelfHostLlm.Domain.Routing;

/// <summary>Đầu việc mà một virtual model phục vụ; dùng cho explicit task routing.</summary>
public enum TaskKind
{
    Chat,
    Coding,
    Embedding,
    Summarization,
    Classification,
}
