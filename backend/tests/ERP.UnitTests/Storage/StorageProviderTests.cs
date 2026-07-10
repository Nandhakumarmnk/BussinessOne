using ERP.Application.Common.Interfaces;
using ERP.Infrastructure;
using ERP.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.UnitTests.Storage;

public class StorageProviderTests
{
    private static ServiceDescriptor ResolveStorageDescriptor(params (string key, string value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.key, s.value)))
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(config);

        // Inspect the registration WITHOUT resolving — so GcsFileStorage's credential-reading
        // constructor never runs and no GCP access is attempted during the test.
        return services.Single(d => d.ServiceType == typeof(IFileStorage));
    }

    [Fact]
    public void Default_provider_is_local()
        => ResolveStorageDescriptor().ImplementationType.Should().Be(typeof(LocalFileStorage));

    [Fact]
    public void Local_provider_selects_local_storage()
        => ResolveStorageDescriptor(("Storage:Provider", "Local"))
            .ImplementationType.Should().Be(typeof(LocalFileStorage));

    [Fact]
    public void Firebase_provider_selects_gcs_storage()
        => ResolveStorageDescriptor(("Storage:Provider", "Firebase"))
            .ImplementationType.Should().Be(typeof(GcsFileStorage));

    [Fact]
    public void Provider_selection_is_case_insensitive()
        => ResolveStorageDescriptor(("Storage:Provider", "firebase"))
            .ImplementationType.Should().Be(typeof(GcsFileStorage));

    [Fact]
    public async Task Local_storage_returns_portable_key_and_relative_download_url()
    {
        var root = Path.Combine(Path.GetTempPath(), "erp-storage-test-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("Files:Root", root) })
            .Build();
        var storage = new LocalFileStorage(config);

        await using var content = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var key = await storage.SaveAsync(content, "receipt.png", "image/png", "expenses");

        // Same scheme GcsFileStorage uses: "<folder>/<32-hex-guid>_<name>".
        key.Should().MatchRegex(@"^expenses/[0-9a-f]{32}_receipt\.png$");
        File.Exists(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();

        var url = await storage.GetDownloadUrlAsync(key);
        url.Should().Be($"/api/v1/files/content?key={Uri.EscapeDataString(key)}");

        Directory.Delete(root, recursive: true);
    }
}
