var builder = DistributedApplication.CreateBuilder(args);

// Add Redis container for message streaming
var redis = builder.AddRedis("redis")
    .WithDataVolume(); // Persist data across container restarts

builder.Build().Run();
