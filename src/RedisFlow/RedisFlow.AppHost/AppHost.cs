var builder = DistributedApplication.CreateBuilder(args);

// Add Redis for stream processing
var redis = builder.AddRedis("redis");

builder.Build().Run();
