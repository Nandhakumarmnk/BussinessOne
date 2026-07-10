using ERP.Application.Common.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Storage;

/// <summary>
/// Production file storage backed by a Firebase Storage bucket (a Google Cloud Storage bucket).
/// Uploads objects and issues short-lived, offline-signed V4 download URLs from the service-account
/// private key — so no extra IAM (signBlob) permission is needed. Registered as a singleton; the
/// <see cref="StorageClient"/> and <see cref="UrlSigner"/> are thread-safe and built once.
/// </summary>
public sealed class GcsFileStorage : IFileStorage
{
    private readonly StorageClient _client;
    private readonly UrlSigner _signer;
    private readonly string _bucket;
    private readonly TimeSpan _urlLifetime;

    public GcsFileStorage(IConfiguration config)
    {
        _bucket = config["Storage:Firebase:Bucket"]
            ?? throw new InvalidOperationException(
                "Storage:Firebase:Bucket must be set when Storage:Provider is 'Firebase'.");
        _urlLifetime = TimeSpan.FromMinutes(config.GetValue("Storage:Firebase:SignedUrlMinutes", 15));

        // Credential resolution: inline JSON (env/secret) → explicit file path → Application Default
        // Credentials (GOOGLE_APPLICATION_CREDENTIALS / metadata server). Offline signing requires a
        // service-account credential, so provide the SA JSON on the VM rather than relying on ADC.
        var inlineJson = config["Storage:Firebase:CredentialsJson"];
        var path = config["Storage:Firebase:CredentialsPath"];

        var credential =
            !string.IsNullOrWhiteSpace(inlineJson) ? GoogleCredential.FromJson(inlineJson)
            : !string.IsNullOrWhiteSpace(path) ? GoogleCredential.FromFile(path)
            : GoogleCredential.GetApplicationDefault();

        if (credential.UnderlyingCredential is not ServiceAccountCredential saCredential)
            throw new InvalidOperationException(
                "Firebase storage needs a service-account credential to sign download URLs. " +
                "Set Storage:Firebase:CredentialsJson or CredentialsPath to a service-account key.");

        _signer = UrlSigner.FromCredential(saCredential);
        _client = StorageClient.Create(credential);
    }

    public async Task<string> SaveAsync(
        Stream content, string fileName, string contentType, string? folder, CancellationToken ct = default)
    {
        // Same key scheme as LocalFileStorage so stored keys are portable between providers.
        var safeName = Path.GetFileName(fileName);
        var key = $"{folder ?? "misc"}/{Guid.NewGuid():N}_{safeName}";
        await _client.UploadObjectAsync(_bucket, key, contentType, content, cancellationToken: ct);
        return key;
    }

    public Task<string> GetDownloadUrlAsync(string objectKey, CancellationToken ct = default)
        => _signer.SignAsync(_bucket, objectKey, _urlLifetime, HttpMethod.Get, cancellationToken: ct);
}
