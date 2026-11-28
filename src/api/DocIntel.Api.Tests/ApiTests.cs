using System.Net;
using System.Net.Http.Json;

namespace DocIntel.Api.Tests;

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_is_anonymous()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_refuses_an_anonymous_caller()
    {
        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var me = await factory.ClientFor(TestUsers.Reader).GetFromJsonAsync<Me>("/api/me");

        Assert.Equal("Test Reader", me!.Name);
        Assert.Equal(["gid-readers"], me.Groups);
    }

    [Fact]
    public async Task Cors_allows_the_web_origin()
    {
        var response = await Preflight(ApiFactory.WebOrigin);

        Assert.Equal(ApiFactory.WebOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Cors_ignores_other_origins()
    {
        var response = await Preflight("https://elsewhere.example");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private Task<HttpResponseMessage> Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/me");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return factory.CreateClient().SendAsync(request);
    }

    private sealed record Me(string? Name, string? ObjectId, string[] Groups);
}
