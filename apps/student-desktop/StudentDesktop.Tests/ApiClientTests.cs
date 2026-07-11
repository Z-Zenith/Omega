using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using StudentDesktop.Services;
using Xunit;

namespace StudentDesktop.Tests;

public class ApiClientTests
{
    private class FakeHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body) });
    }

    private static ApiClient NewClientWithFakeResponse(HttpStatusCode statusCode, string body)
    {
        var client = new ApiClient();
        var httpClient = new HttpClient(new FakeHandler(statusCode, body)) { BaseAddress = new System.Uri("http://localhost:8080") };
        typeof(ApiClient).GetField("_http", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, httpClient);
        return client;
    }

    // #158 — the backend returns {"error": "...", "message": "human text"} on failure;
    // ApiException.Message must surface that human text, not the raw JSON blob.
    [Fact]
    public async Task LoginAsync_SurfacesBackendMessage_NotRawJsonBody()
    {
        var client = NewClientWithFakeResponse(
            HttpStatusCode.Unauthorized,
            "{\"error\":\"invalid_password\",\"message\":\"Incorrect password.\"}");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("101", "wrong", "000000"));

        Assert.Equal("Incorrect password.", ex.Message);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_FallsBackToRawBody_WhenResponseIsNotJson()
    {
        var client = NewClientWithFakeResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("101", "wrong", "000000"));

        Assert.Equal("Internal Server Error", ex.Message);
    }
}
