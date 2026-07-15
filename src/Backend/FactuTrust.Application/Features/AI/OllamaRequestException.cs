namespace FactuTrust.Application.Features.AI;

/// <summary>
/// Raised when Ollama returns a non-success response; carries a user-safe French message.
/// </summary>
public sealed class OllamaRequestException : Exception
{
    public string UserMessage { get; }
    public int? HttpStatusCode { get; }

    public OllamaRequestException(string userMessage, string? technicalDetail = null, Exception? inner = null, int? httpStatusCode = null)
        : base(technicalDetail ?? userMessage, inner)
    {
        UserMessage = userMessage;
        HttpStatusCode = httpStatusCode;
    }
}
