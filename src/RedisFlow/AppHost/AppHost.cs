var builder = DistributedApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var redis = builder.AddRedis("redis");


builder.Build().Run();
