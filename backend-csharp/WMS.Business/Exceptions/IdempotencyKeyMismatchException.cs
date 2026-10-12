namespace WMS.Business.Exceptions;

public class IdempotencyKeyMismatchException : Exception
{
    public string Key { get; }

    public IdempotencyKeyMismatchException(string key, string message = "Idempotency key reused with different request body")
        : base(message)
    {
        Key = key;
    }
}
