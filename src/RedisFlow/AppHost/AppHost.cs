IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

builder.Build().Run();