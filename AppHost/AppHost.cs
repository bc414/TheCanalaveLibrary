using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Stable dev credentials live in AppHost user secrets (Parameters:postgres-password /
// Parameters:garage-s3-secret / Parameters:garage-rpc-secret) - machine-local, never committed.
// Stability matters because the containers keep data volumes: a regenerated secret would no
// longer match the already-initialized state inside the volume.
IResourceBuilder<ParameterResource> postgresPassword = builder.AddParameter("postgres-password", secret: true);
IResourceBuilder<ParameterResource> garageS3Secret = builder.AddParameter("garage-s3-secret", secret: true);
IResourceBuilder<ParameterResource> garageRpcSecret = builder.AddParameter("garage-rpc-secret", secret: true);

// Garage's S3 access-key ID (key IDs are "GK" + 32 hex by convention; not a secret - the
// secret half is the parameter above). Shared between the container bootstrap and the web app.
const string garageAccessKey = "GKa1b2c3d4e5f60718293a4b5c6d7e8f90";
const string imagesBucket = "canalave-images";

// All three backing services are persistent-lifetime containers with named volumes: they keep
// running (and keep data) across AppHost restarts, so day-to-day starts are fast and the Aspire
// dev DB is a persistent workbench like the server-only path's (run-server/SKILL.md "Dev DB
// lifecycle" applies to both). Wipe = scripts/reset-aspire-db.ps1.

// Postgres pinned to major 18 only: minors are on-disk compatible with the volume, majors are
// not (a major bump requires reset-aspire-db.ps1). Host port 5433 - local PostgreSQL 18 owns 5432.
IResourceBuilder<PostgresDatabaseResource> canalaveDb = builder.AddPostgres("postgres", password: postgresPassword)
    .WithImageTag("18")
    .WithContainerName("canalave-postgres")
    .WithDataVolume("canalave-postgres-data")
    .WithHostPort(5433)
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("canalavedb");

// Resource name "cache" is the connection-string name the L7 client registration will consume
// (builder.AddRedisDistributedCache("cache") - see Server/Program.cs comment, layer7-redis.md).
IResourceBuilder<RedisResource> cache = builder.AddRedis("cache")
    .WithContainerName("canalave-redis")
    .WithDataVolume("canalave-redis-data")
    .WithPersistence(TimeSpan.FromMinutes(5), 100)
    .WithHostPort(6379)
    .WithLifetime(ContainerLifetime.Persistent);

// Garage (S3-compatible dev blob store; Cloudflare R2 in prod - same AWS SDK code + wire
// format, different endpoint, see audit/ImageStorage.md). Supersedes the spec's MinIO (OSS
// archived 2026-02; settled 2026-07-05, middle_plan Resolved). v2.3.0's --single-node
// --default-bucket bootstraps layout + access key + bucket from the GARAGE_DEFAULT_* env vars,
// zero interactive setup. S3 API on 3900 (no web console; inspect via
// `docker exec canalave-garage /garage bucket info canalave-images`).
IResourceBuilder<ContainerResource> garage = builder.AddContainer("garage", "dxflrs/garage", "v2.3.0")
    .WithArgs("/garage", "server", "--single-node", "--default-bucket")
    .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", garageAccessKey)
    .WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", garageS3Secret)
    .WithEnvironment("GARAGE_DEFAULT_BUCKET", imagesBucket)
    .WithEnvironment("GARAGE_RPC_SECRET", garageRpcSecret)
    .WithBindMount("garage.toml", "/etc/garage.toml", isReadOnly: true)
    .WithContainerName("canalave-garage")
    .WithVolume("canalave-garage-meta", "/var/lib/garage/meta")
    .WithVolume("canalave-garage-data", "/var/lib/garage/data")
    .WithHttpEndpoint(port: 3900, targetPort: 3900, name: "s3")
    .WithLifetime(ContainerLifetime.Persistent);

// launchProfileName pins the web app to Server's "http" profile -> same http://localhost:5028
// as the server-only path, so every verification flow (curl, browser tools, DevLoginBar, the
// scripts' port checks) is identical under either path. WithReference injects
// ConnectionStrings__canalavedb / ConnectionStrings__cache as env vars, which override
// appsettings.Development.json - the Server needs no Aspire client packages for that
// (plain AddDbContext + GetConnectionString stays, per layer2-services.md).
builder.AddProject<Projects.TheCanalaveLibrary_Server>("web", launchProfileName: "http")
    .WithReference(canalaveDb)
    .WithReference(cache)
    // S3 image storage (WU-S3Garage): flips the provider switch in Server/Program.cs and hands
    // it the Garage endpoint + credentials. Region must match garage.toml's s3_region.
    .WithEnvironment("ImageStorage__Provider", "S3")
    .WithEnvironment("ImageStorage__S3__ServiceUrl", garage.GetEndpoint("s3"))
    .WithEnvironment("ImageStorage__S3__Region", "garage")
    .WithEnvironment("ImageStorage__S3__AccessKey", garageAccessKey)
    .WithEnvironment("ImageStorage__S3__SecretKey", garageS3Secret)
    .WithEnvironment("ImageStorage__S3__BucketName", imagesBucket)
    .WaitFor(canalaveDb)
    .WaitFor(garage);

builder.Build().Run();
