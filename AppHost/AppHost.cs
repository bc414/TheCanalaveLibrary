using Aspire.Hosting.ApplicationModel;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<RedisResource> cache = builder.AddRedis("cache");

// This adds your web server project to the AppHost.
// After you rename the project to "TheCanalaveLibrary.Web",
// you will need to update this line to reflect the new name, like this:
// var web = builder.AddProject<Projects.TheCanalaveLibrary_Web>("thecanalavelibrary-web");
IResourceBuilder<ProjectResource> web = builder.AddProject<Projects.TheCanalaveLibrary>("thecanalavelibrary-web")
    .WithReference(cache);

builder.Build().Run();
