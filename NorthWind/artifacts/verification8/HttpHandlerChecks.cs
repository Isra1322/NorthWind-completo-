using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NorthWind.HttpDelegatingHandlers;
using NorthWind.Validation.Entities.ValueObjects;

internal static class HttpHandlerChecks
{
    private static readonly List<object> Results = new();

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static async Task Check(string name, HttpStatusCode status, string body,
        string? mediaType, string expected, string? source = null, bool errors = false)
    {
        using var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
        response.Content.Headers.ContentType = mediaType is null ? null : new MediaTypeHeaderValue(mediaType);
        using var client = new HttpClient(new ExceptionDelegatingHandler
        {
            InnerHandler = new ResponseFixtureHandler(response)
        });
        try
        {
            using var result = await client.GetAsync("https://fixture.invalid/");
            Assert((int)status < 300 && ReferenceEquals(result, response), "Una respuesta exitosa debe conservarse");
            Assert(await result.Content.ReadAsStringAsync() == body, "Se modificó la respuesta exitosa");
        }
        catch (HttpRequestException exception)
        {
            Assert(exception.StatusCode == status, "Código HTTP perdido");
            Assert(exception.Message == expected, $"Mensaje inesperado: {exception.Message}");
            Assert(exception.Source == (source ?? "NorthWind.HttpDelegatingHandlers"), $"Source inesperado: {exception.Source}");
            if (errors)
            {
                var items = (exception.Data["Errors"] as IEnumerable<ValidationError>)?.ToArray();
                Assert(items is { Length: 1 } && items[0].PropertyName == "CustomerId" && items[0].Message == "Cliente no existe", "Errores estructurados perdidos");
            }
            else Assert(!exception.Data.Contains("Errors"), "Colección de errores inesperada");
        }
        Results.Add(new { name, passed = true, status = (int)status });
    }

    public static async Task Main()
    {
        const string problem = "{\"INSTANCE\":\"ProblemDetails/ValidationException\",\"TITLE\":\"Entrada inválida.\",\"DETAIL\":\"Corrija los datos.\",\"ERRORS\":[{\"PROPERTYNAME\":\"CustomerId\",\"MESSAGE\":\"Cliente no existe\"}]}";
        await Check("Success unchanged", HttpStatusCode.OK, "42", "application/json", "42");
        await Check("Unauthorized", HttpStatusCode.Unauthorized, problem, "application/problem+json", "401 Unauthorized");
        await Check("Forbidden", HttpStatusCode.Forbidden, "private detail", "text/plain", "403 Forbidden");
        await Check("Case-insensitive ProblemDetails and errors", HttpStatusCode.BadRequest, problem,
            "APPLICATION/PROBLEM+JSON", "Entrada inválida. Corrija los datos.", "ValidationException", true);
        await Check("Malformed JSON preserves body", HttpStatusCode.BadRequest, "{not json", "application/problem+json", "{not json");
        await Check("Text preserves body", HttpStatusCode.InternalServerError, "Error original", "text/plain", "Error original");
        await Check("Missing content type", HttpStatusCode.BadGateway, "Original sin encabezado", null, "Original sin encabezado");
        await Check("Empty content", HttpStatusCode.BadGateway, "", null, "502 Bad Gateway");
        await Check("Array instead of problem", HttpStatusCode.BadRequest, "[]", "application/problem+json", "[]");
        await Check("Wrong prefix preserves body", HttpStatusCode.BadRequest, "{\"instance\":\"Other/Exception\",\"title\":\"Error\"}", "application/problem+json", "{\"instance\":\"Other/Exception\",\"title\":\"Error\"}");
        await Check("JSON without problem media type", HttpStatusCode.BadRequest, problem, "application/json", problem);
        await Check("Missing instance", HttpStatusCode.BadRequest, "{\"title\":\"Error\"}", "application/problem+json", "{\"title\":\"Error\"}");
        await Check("Update without validation errors", HttpStatusCode.InternalServerError, "{\"instance\":\"ProblemDetails/UpdateException\",\"title\":\"Error de actualización.\",\"detail\":\"Consulte al administrador.\"}", "application/problem+json", "Error de actualización. Consulte al administrador.", "UpdateException");
        File.WriteAllText("artifacts/verification8/http-handler-results.json", JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{Results.Count} comprobaciones del handler HTTP aprobadas.");
    }

    // Transporte de prueba: permite ejercitar el handler real con respuestas HTTP controladas.
    private sealed class ResponseFixtureHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }
    }
}
