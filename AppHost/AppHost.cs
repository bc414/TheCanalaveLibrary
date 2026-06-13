using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<RedisResource> cache = builder.AddRedis("cache");

IResourceBuilder<IResourceWithConnectionString> defaultConnection = builder.AddConnectionString("DefaultConnection");

// This adds your web server project to the AppHost.
IResourceBuilder<ProjectResource> web = builder.AddProject<Projects.TheCanalaveLibrary_Server>("thecanalavelibrary-web")
    .WithReference(cache)
    .WithReference(defaultConnection);

builder.Build().Run();
