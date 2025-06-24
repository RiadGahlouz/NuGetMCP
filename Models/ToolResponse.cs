public enum ToolResponseResult
{
  Success,
  PartialSuccess,
  Failure
}

public record ToolResponse<T>(ToolResponseResult Result, T? Payload, string? ErrorMessage = null)
{

  public static ToolResponse<T> Success(T? payload = default) => new(ToolResponseResult.Success, payload);
  public static ToolResponse<T> PartialSuccess(string errorMessage, T? payload = default) => new(ToolResponseResult.PartialSuccess, payload, errorMessage);
  public static ToolResponse<T> Failure(string errorMessage) => new(ToolResponseResult.Failure, default, errorMessage);
}