using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace RedisFlow.Integration;

[TestFixture]
public class AppHostConfigurationTests
{
    [Test]
    public async Task Should_DefineRedisResource_When_AppHostIsBuilt()
    {
        // Given / When
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.RedisFlow_AppHost>();

        var app = await appHost.BuildAsync();

        // Then
        var redisResource = app.Services.GetServices<IResource>()
            .FirstOrDefault(r => r.Name == "redis");

        redisResource.Should().NotBeNull("because the AppHost should define a 'redis' resource");
    }

    [Test]
    public void Should_HaveRedisResourceDefined_When_CheckingApplicationModel()
    {
        // Given
        var builder = DistributedApplication.CreateBuilder();
        
        // When
        var redis = builder.AddRedis("redis");

        // Then
        redis.Should().NotBeNull("because AddRedis should return a resource builder");
        redis.Resource.Name.Should().Be("redis", "because we specified 'redis' as the resource name");
    }
}
