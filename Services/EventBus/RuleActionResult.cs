namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// The outcome of one run of a rule action, returned by <see cref="Interfaces.IRuleActionHandler.InvokeAsync"/>.
/// </summary>
/// <remarks>
/// The action worker uses <see cref="Status"/> to decide what happens next: mark the action done,
/// retry it with backoff, or move it to dead-letter. <see cref="ErrorMessage"/> and
/// <see cref="ErrorType"/> are persisted on the action row; <see cref="Exception"/> is for in-process
/// logging only and is never persisted.
/// </remarks>
public sealed record RuleActionResult
{
    private RuleActionResult(RuleActionStatus status, Guid? jobId = null, string? errorMessage = null,
        string? errorType = null, Exception? exception = null)
    {
        Status = status;
        JobId = jobId;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
        Exception = exception;
    }

    /// <summary>
    /// The action succeeded.
    /// </summary>
    /// <param name="jobId">
    /// The device job the action enqueued, for device actions. Leave <see langword="null"/> for
    /// service actions.
    /// </param>
    public static RuleActionResult Ok(Guid? jobId = null) => new(RuleActionStatus.Success, jobId);

    /// <summary>
    /// A transient problem, such as an unreachable service. The worker retries with backoff.
    /// </summary>
    /// <param name="message">What went wrong. Persisted on the action row.</param>
    public static RuleActionResult Retry(string message) =>
        new(RuleActionStatus.RetryableFailure, errorMessage: message);

    /// <summary>
    /// A problem retrying cannot fix, such as invalid settings or a missing target. The action goes
    /// straight to dead-letter.
    /// </summary>
    /// <param name="message">What went wrong. Persisted on the action row.</param>
    public static RuleActionResult Fail(string message) =>
        new(RuleActionStatus.PermanentFailure, errorMessage: message);

    /// <summary>
    /// An unexpected exception. Treated as retryable.
    /// </summary>
    /// <param name="ex">
    /// The exception. Its message and full type name are persisted; the exception itself is kept
    /// only for in-process logging. Map an <see cref="OperationCanceledException"/> caused by the run's
    /// cancellation token to <see cref="Cancelled"/> instead.
    /// </param>
    public static RuleActionResult ExceptionThrown(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return new(RuleActionStatus.RetryableFailure, errorMessage: ex.Message,
            errorType: ex.GetType().FullName, exception: ex);
    }

    /// <summary>
    /// Handing off the work timed out. Treated as retryable.
    /// </summary>
    public static RuleActionResult TimedOut() =>
        new(RuleActionStatus.Timeout, errorMessage: "The action timed out.");

    /// <summary>
    /// The run was cancelled, for example by shutdown. Not counted as a failed attempt.
    /// </summary>
    public static RuleActionResult Cancelled() => new(RuleActionStatus.Cancelled);

    /// <summary>
    /// How the run ended. Decides whether the worker completes, retries or dead-letters the action.
    /// </summary>
    public RuleActionStatus Status { get; }

    /// <summary>
    /// The device job a device action enqueued. <see langword="null"/> for service actions and for
    /// any result other than <see cref="RuleActionStatus.Success"/>.
    /// </summary>
    /// <remarks>Lets an event be traced through its action to the job it produced.</remarks>
    public Guid? JobId { get; }

    /// <summary>
    /// A description of the failure. Persisted on the action row.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// The full type name of the exception that caused the failure, when there was one. Persisted on
    /// the action row.
    /// </summary>
    public string? ErrorType { get; }

    /// <summary>
    /// The exception that caused the failure, for in-process logging only. Never persisted.
    /// </summary>
    public Exception? Exception { get; }
}

/// <summary>
/// How a run of a rule action ended. The action worker decides what to do next from this.
/// </summary>
public enum RuleActionStatus
{
    /// <summary>
    /// The action completed.
    /// </summary>
    Success,

    /// <summary>
    /// A transient problem, such as an unreachable service. The worker retries with backoff until the
    /// maximum number of attempts, then dead-letters the action.
    /// </summary>
    RetryableFailure,

    /// <summary>
    /// Retrying cannot help, for example invalid settings or a missing target. The action goes straight
    /// to dead-letter.
    /// </summary>
    PermanentFailure,

    /// <summary>
    /// Handing off the work timed out. Treated as retryable.
    /// </summary>
    Timeout,

    /// <summary>
    /// The run was cancelled, for example by shutdown. Not counted as a failed attempt.
    /// </summary>
    Cancelled,
}
