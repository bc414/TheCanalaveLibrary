using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<RedisResource> cache = builder.AddRedis("cache");

IResourceBuilder<PostgresDatabaseResource> canalaveDb = builder.AddPostgres("postgres").AddDatabase("canalavedb");

// This adds your web server project to the AppHost.
IResourceBuilder<ProjectResource> web = builder.AddProject<Projects.TheCanalaveLibrary_Server>("thecanalavelibrary-web")
    .WithReference(cache)
    .WithReference(canalaveDb);

builder.Build().Run();
