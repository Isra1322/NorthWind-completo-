namespace NorthWind.HttpDelegatingHandlers;

public class ExceptionDelegatingHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage Response = await base.SendAsync(request, cancellationToken);
        if (Response.IsSuccessStatusCode) return Response;

        try
        {
            string content = await Response.Content.ReadAsStringAsync(cancellationToken);
            string Message = string.IsNullOrEmpty(content)
                ? $"{(int)Response.StatusCode} {Response.ReasonPhrase}"
                : content;
            string? Source = null;
            IEnumerable<ValidationError>? Errors = null;

            if (Response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Message = $"{(int)Response.StatusCode} {Response.ReasonPhrase}";
            }
            else if (string.Equals(Response.Content.Headers.ContentType?.MediaType,
                "application/problem+json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var document = JsonDocument.Parse(content);
                    var root = document.RootElement;
                    if (TryGetProperty(root, "instance", out var instance) &&
                        instance.ValueKind == JsonValueKind.String &&
                        instance.GetString() is string instanceValue &&
                        instanceValue.StartsWith("ProblemDetails/", StringComparison.OrdinalIgnoreCase))
                    {
                        string? title = TryGetProperty(root, "title", out var titleElement) &&
                            titleElement.ValueKind == JsonValueKind.String ? titleElement.GetString() : null;
                        string? detail = TryGetProperty(root, "detail", out var detailElement) &&
                            detailElement.ValueKind == JsonValueKind.String ? detailElement.GetString() : null;
                        if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(detail))
                        {
                            Message = string.Join(" ", new[] { title, detail }
                                .Where(value => !string.IsNullOrEmpty(value)));
                            Source = instanceValue["ProblemDetails/".Length..];
                            if (TryGetProperty(root, "errors", out var errorsElement) &&
                                errorsElement.ValueKind == JsonValueKind.Array)
                                Errors = errorsElement.Deserialize<ValidationError[]>(JsonOptions)?
                                    .Where(error => error is not null).ToArray();
                        }
                    }
                }
                catch (JsonException)
                {
                    // El cuerpo original sigue siendo el mensaje si no es un ProblemDetails válido.
                    Message = string.IsNullOrEmpty(content)
                        ? $"{(int)Response.StatusCode} {Response.ReasonPhrase}" : content;
                    Source = null;
                    Errors = null;
                }
            }

            var Ex = new HttpRequestException(Message, null, Response.StatusCode)
            {
                Source = Source
            };
            if (Errors is not null) Ex.Data.Add("Errors", Errors);
            if (Response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.NotFound &&
                string.Equals(Response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var document = JsonDocument.Parse(content);
                    if (TryGetProperty(document.RootElement, "message", out var message) && message.ValueKind == JsonValueKind.String)
                        Ex.Data["UserMessage"] = message.GetString();
                }
                catch (JsonException) { }
            }
            throw Ex;
        }
        finally
        {
            Response.Dispose();
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}
