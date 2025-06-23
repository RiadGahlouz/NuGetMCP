public record ToolResponse<T>(T Result, string? ErrorMessage = null)
{
  public bool IsSuccess => ErrorMessage == null;

  public static ToolResponse<T> Success(T result) => new(result);

  public static ToolResponse<T> Failure(string errorMessage) => new(default!, errorMessage);
}

public record BooleanToolResponse(bool Result, string? ErrorMessage = null) : ToolResponse<bool>(Result, ErrorMessage)
{
  public static BooleanToolResponse Success() => new(true);

  public static new BooleanToolResponse Failure(string errorMessage) => new(false, errorMessage);
}