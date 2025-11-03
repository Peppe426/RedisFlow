var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

builder.Build().Run();
