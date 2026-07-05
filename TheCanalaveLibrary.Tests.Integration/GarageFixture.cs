using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Spins up a throwaway single-node Garage container (same image + bootstrap flags as the Aspire
/// AppHost's <c>canalave-garage</c> resource) for <see cref="S3ImageStorageServiceTests"/>.
/// <c>--single-node --default-bucket</c> auto-creates the layout, the access key from the
/// <c>GARAGE_DEFAULT_*</c> env vars, and the bucket — no interactive setup. No volumes: test blobs
/// are deliberately ephemeral. Distinct from <see cref="PostgresFixture"/>'s collection — these
/// tests never touch the database.
/// </summary>
public sealed class GarageFixture : IAsyncLifetime
{
    // Fixed test credentials, same shapes as the AppHost's (key ID "GK" + 32 hex; 64-hex secret).
    public const string AccessKey = "GK00112233445566778899aabbccddeeff";
    public const string SecretKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    public const string BucketName = "canalave-test-images";

    private IContainer? _container;

    public S3ImageStorageOptions Options { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("dxflrs/garage:v2.3.0")
            .WithCommand("/garage", "server", "--single-node", "--default-bucket")
            .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", AccessKey)
            .WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", SecretKey)
            .WithEnvironment("GARAGE_DEFAULT_BUCKET", BucketName)
            .WithEnvironment("GARAGE_RPC_SECRET", "feedfacefeedfacefeedfacefeedfacefeedfacefeedfacefeedfacefeedface")
            .WithResourceMapping(GarageToml(), "/etc/garage.toml")
            .WithPortBinding(3900, assignRandomHostPort: true)
            // No wait strategy beyond "running": readiness is gated below on the S3 API actually
            // answering for the bootstrapped bucket, which is the condition tests really need.
            .Build();

        await _container.StartAsync();

        Options = new S3ImageStorageOptions
        {
            ServiceUrl = $"http://localhost:{_container.GetMappedPublicPort(3900)}",
            Region = "garage", // must equal the s3_region below
            AccessKey = AccessKey,
            SecretKey = SecretKey,
            BucketName = BucketName,
        };

        // The port opens before --default-bucket bootstrap finishes — wait until the bucket
        // actually answers before handing the fixture to tests.
        using AmazonS3Client client = S3ImageStorageService.CreateClient(Options);
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                await client.GetBucketLocationAsync(BucketName);
                break;
            }
            catch (AmazonS3Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static byte[] GarageToml() => System.Text.Encoding.UTF8.GetBytes(
        """
        metadata_dir = "/var/lib/garage/meta"
        data_dir = "/var/lib/garage/data"
        db_engine = "sqlite"
        replication_factor = 1
        rpc_bind_addr = "[::]:3901"
        rpc_public_addr = "127.0.0.1:3901"

        [s3_api]
        s3_region = "garage"
        api_bind_addr = "[::]:3900"
        root_domain = ".s3.garage.localhost"
        """);
}
