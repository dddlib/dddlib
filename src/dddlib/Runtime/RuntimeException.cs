namespace dddlib.Runtime;

/// <summary>
/// Represents a runtime exception: a problem with how the domain model is defined or configured, rather than a
/// violation of a business rule. When <see cref="Exception.HelpLink"/> is set it is appended to the message.
/// </summary>
public class RuntimeException : Exception
{
    private const string DefaultMessage = "A runtime exception has occurred.";

    private readonly string? originalMessage;

    public RuntimeException()
        : this(DefaultMessage, null)
    {
    }

    public RuntimeException(string? message)
        : this(message, null)
    {
    }

    public RuntimeException(Exception? innerException)
        : this(DefaultMessage, innerException)
    {
    }

    public RuntimeException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        this.originalMessage = message;
    }

    public override string Message =>
        this.HelpLink is not { } helpLink
            ? base.Message
            : string.IsNullOrWhiteSpace(this.originalMessage)
                ? helpLink
                : string.Concat(this.originalMessage, "\r\nFurther information: ", helpLink);
}
