using System.Text.Json.Serialization;

namespace Handler
{
    public class DataResponse<T>
    {
        [JsonPropertyName("data")] public T Data { get; init; } = default!;
    }

    public class ListResponse<T>
    {
        [JsonPropertyName("data")] public IEnumerable<T> Data { get; init; } = [];
        [JsonPropertyName("meta")] public PageMeta Meta { get; init; } = new();
    }

    public class PageMeta
    {
        [JsonPropertyName("page")] public int Page { get; init; }
        [JsonPropertyName("limit")] public int Limit { get; init; }
        [JsonPropertyName("total")] public int Total { get; init; }
    }

    public class ErrorResponse
    {
        [JsonPropertyName("error")] public ErrorBody Error { get; init; } = new();
    }

    public class ErrorBody
    {
        [JsonPropertyName("code")] public string Code { get; init; } = string.Empty;
        [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;

        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IEnumerable<ErrorDetail>? Details { get; init; }
    }

    public class ErrorDetail
    {
        [JsonPropertyName("field")] public string Field { get; init; } = string.Empty;
        [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    }
}
