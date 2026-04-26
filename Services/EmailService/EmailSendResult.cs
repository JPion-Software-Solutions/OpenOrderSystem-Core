namespace OpenOrderSystem.Core.Services.EmailService;

public class EmailSendResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}