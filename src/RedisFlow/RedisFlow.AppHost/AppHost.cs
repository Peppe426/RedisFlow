var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);
// Add Redis resource for local development
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);
// Add Redis resource
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// Add producer applications
builder.AddProject<Projects.RedisFlow_Producer1>("producer1")
    .WithReference(redis)
    .WaitFor(redis);

builder.AddProject<Projects.RedisFlow_Producer2>("producer2")
    .WithReference(redis)
    .WaitFor(redis);

builder.Build().Run();
