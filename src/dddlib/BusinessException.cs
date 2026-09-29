namespace dddlib;

/// <summary>
/// Represents a business exception: a violation of a rule of the domain model.
/// </summary>
public class BusinessException : Exception
{
    public BusinessException()
    {
    }

    public BusinessException(string? message)
        : base(message)
    {
    }

    public BusinessException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
