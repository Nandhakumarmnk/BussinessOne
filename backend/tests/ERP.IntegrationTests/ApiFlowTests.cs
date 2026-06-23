using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ERP.IntegrationTests;

public class ApiFlowTests : IClassFixture<ErpWebAppFactory>
{
    private readonly ErpWebAppFactory _factory;
    public ApiFlowTests(ErpWebAppFactory factory) => _factory = factory;

    // ---- helpers ----

    private static string UniqueMobile() => "9" + Guid.NewGuid().ToString("N")[..9];

    private async Task<(string token, string businessId)> LoginSeededOwnerAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { mobileOrEmail = "owner@business-one.local", password = "Owner@123" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadTokenAndBusiness(res);
    }

    private async Task<(string token, string businessId)> RegisterTenantAsync(HttpClient client)
    {
        var suffix = UniqueMobile();
        var res = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            tenantName = $"T-{suffix}",
            fullName = $"Owner {suffix}",
            mobile = suffix,
            email = $"{suffix}@example.com",
            password = "Owner@123",
            firstBusinessName = "Biz",
            firstBusinessTypeCode = "TRANSPORT"
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadTokenAndBusiness(res);
    }

    private static async Task<(string, string)> ReadTokenAndBusiness(HttpResponseMessage res)
    {
        var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        return (data.GetProperty("accessToken").GetString()!,
                data.GetProperty("memberships")[0].GetProperty("businessId").GetString()!);
    }

    private HttpClient AuthedClient(string token, string businessId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Business-Id", businessId);
        return client;
    }

    // ---- tests ----

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health/live");
        res.Headers.TryGetValues("X-Content-Type-Options", out var values).Should().BeTrue();
        values!.Should().Contain("nosniff");
    }

    [Fact]
    public async Task Login_returns_token_and_membership()
    {
        var client = _factory.CreateClient();
        var (token, businessId) = await LoginSeededOwnerAsync(client);
        token.Should().NotBeNullOrEmpty();
        businessId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_with_wrong_password_is_401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { mobileOrEmail = "owner@business-one.local", password = "wrong" });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_without_token_is_401()
    {
        var client = _factory.CreateClient();
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_token_returns_profile()
    {
        var client = _factory.CreateClient();
        var (token, _) = await LoginSeededOwnerAsync(client);
        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await authed.GetAsync("/api/v1/me");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        data.GetProperty("user").GetProperty("fullName").GetString().Should().Be("Demo Owner");
    }

    [Fact]
    public async Task Create_expense_then_list_returns_it()
    {
        var anon = _factory.CreateClient();
        var (token, businessId) = await LoginSeededOwnerAsync(anon);
        var client = AuthedClient(token, businessId);

        var create = await client.PostAsJsonAsync("/api/v1/expenses",
            new { expenseDate = "2026-06-23", amount = 4200, description = "Diesel" });
        create.IsSuccessStatusCode.Should().BeTrue();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/expenses");
        var rows = list.GetProperty("data");
        rows.GetArrayLength().Should().BeGreaterThan(0);
        rows.EnumerateArray().Should().Contain(e => e.GetProperty("description").GetString() == "Diesel");
    }

    [Fact]
    public async Task Idempotent_expense_post_is_deduped()
    {
        var anon = _factory.CreateClient();
        var (token, businessId) = await RegisterTenantAsync(anon);   // isolated tenant
        var client = AuthedClient(token, businessId);
        var key = Guid.NewGuid().ToString();

        async Task<HttpResponseMessage> Post()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/expenses")
            {
                Content = JsonContent.Create(new { expenseDate = "2026-06-23", amount = 999, description = "Once" })
            };
            req.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(req);
        }

        (await Post()).IsSuccessStatusCode.Should().BeTrue();
        (await Post()).IsSuccessStatusCode.Should().BeTrue();   // replay returns cached response

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/expenses");
        list.GetProperty("data").EnumerateArray()
            .Count(e => e.GetProperty("description").GetString() == "Once")
            .Should().Be(1);   // only applied once
    }

    [Fact]
    public async Task Tenants_are_isolated()
    {
        var anon = _factory.CreateClient();

        var (tokenA, bizA) = await RegisterTenantAsync(anon);
        var clientA = AuthedClient(tokenA, bizA);
        await clientA.PostAsJsonAsync("/api/v1/expenses",
            new { expenseDate = "2026-06-23", amount = 5000, description = "TenantA-only" });

        var (tokenB, bizB) = await RegisterTenantAsync(anon);
        var clientB = AuthedClient(tokenB, bizB);

        var list = await clientB.GetFromJsonAsync<JsonElement>("/api/v1/expenses");
        list.GetProperty("data").EnumerateArray()
            .Should().NotContain(e => e.GetProperty("description").GetString() == "TenantA-only");
    }
}
