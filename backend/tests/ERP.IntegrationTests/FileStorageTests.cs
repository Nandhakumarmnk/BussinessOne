using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>
/// Exercises the file pipeline over the live HTTP stack (default = Local storage provider, no GCP
/// credentials): upload -> store the key on an expense -> resolve an ownership-checked download URL
/// -> stream the bytes back. Also verifies the path-traversal guard on the local content endpoint.
/// </summary>
public class FileStorageTests : IClassFixture<ErpWebAppFactory>
{
    private readonly ErpWebAppFactory _factory;
    public FileStorageTests(ErpWebAppFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedOwnerClientAsync()
    {
        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync("/api/v1/auth/login",
            new { mobileOrEmail = "owner@business-one.local", password = "Owner@123" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var token = data.GetProperty("accessToken").GetString()!;
        var businessId = data.GetProperty("memberships")[0].GetProperty("businessId").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Business-Id", businessId);
        return client;
    }

    [Fact]
    public async Task Upload_attach_then_download_round_trips_the_bytes()
    {
        var client = await AuthedOwnerClientAsync();
        var bytes = Encoding.UTF8.GetBytes("hello-receipt-₹4200");

        // 1) Upload -> objectKey
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "receipt.txt");
        var upload = await client.PostAsync("/api/v1/files?folder=expenses", form);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var objectKey = (await upload.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("objectKey").GetString()!;
        objectKey.Should().MatchRegex(@"^expenses/[0-9a-f]{32}_receipt\.txt$");

        // 2) Create an expense carrying that attachment key
        var create = await client.PostAsJsonAsync("/api/v1/expenses",
            new { expenseDate = "2026-06-23", amount = 4200, description = "With receipt", attachmentKey = objectKey });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var expenseId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetString()!;

        // 3) Ownership-checked download URL (Local provider -> relative content path)
        var attach = await client.GetAsync($"/api/v1/expenses/{expenseId}/attachment");
        attach.StatusCode.Should().Be(HttpStatusCode.OK);
        var url = (await attach.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("url").GetString()!;
        url.Should().StartWith("/api/v1/files/content?key=");

        // 4) Fetch the content -> bytes match
        var download = await client.GetAsync(url);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
    }

    [Fact]
    public async Task Attachment_for_expense_without_one_is_404()
    {
        var client = await AuthedOwnerClientAsync();
        var create = await client.PostAsJsonAsync("/api/v1/expenses",
            new { expenseDate = "2026-06-23", amount = 100, description = "No attachment" });
        var expenseId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetString()!;

        var attach = await client.GetAsync($"/api/v1/expenses/{expenseId}/attachment");
        attach.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Content_endpoint_rejects_path_traversal()
    {
        var client = await AuthedOwnerClientAsync();
        var res = await client.GetAsync("/api/v1/files/content?key=../../appsettings.json");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
