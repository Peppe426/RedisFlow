var builder = DistributedApplication.CreateBuilder(args);

// Add Redis container
var redis = builder.AddRedis("redis");

// Add Producer applications
builder.AddProject<Projects.Producer1>("producer1")
    .WithReference(redis);

builder.AddProject<Projects.Producer2>("producer2")
    .WithReference(redis);

// Add Consumer application
builder.AddProject<Projects.Consumer>("consumer")
    .WithReference(redis);

builder.Build().Run();
