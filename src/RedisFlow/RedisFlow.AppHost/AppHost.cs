var builder = DistributedApplication.CreateBuilder(args);

// Add Redis for stream-based messaging
var redis = builder.AddRedis("redis")
    .WithRedisCommander(); // Add Redis Commander web UI for stream inspection

builder.Build().Run();
