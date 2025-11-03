var builder = DistributedApplication.CreateBuilder(args);

// Add Redis resource for local development
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

builder.Build().Run();
