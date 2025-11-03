var builder = DistributedApplication.CreateBuilder(args);

// Add Redis container
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

builder.Build().Run();
