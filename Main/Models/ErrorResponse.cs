namespace Main.Models;

public record ErrorResponse(ErrorDetail Error)
{
    public static ErrorResponse From(string code, string message, object? details = null) =>
        new(new ErrorDetail(code, message, details));
}

public record ErrorDetail(string Code, string Message, object? Details);
