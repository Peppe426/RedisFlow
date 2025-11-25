using Microsoft.Extensions.DependencyInjection;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<RedisResource> redis = builder.AddRedis("redis");

builder.Build().Run();