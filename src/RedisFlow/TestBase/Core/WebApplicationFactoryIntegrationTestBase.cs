using Microsoft.EntityFrameworkCore;
using TestBase.Exceptions;

namespace TestBase.Core;

public abstract class WebApplicationFactoryIntegrationTestBase<TEntryPoint, TContext> : TestExecutionSettings
    where TEntryPoint : class
    where TContext : DbContext
{
    private readonly WebHostFactory<TEntryPoint, TContext> _webHostFactory = new();
    public HttpClient Client { get; private set; } = null!;

    public TContext DbContext
    {
        get
        {
            if (_webHostFactory.Context is null)
            {
                throw new IntegrationTestException("DbContext is not initialized.");
            }
            return _webHostFactory.Context;
        }
    }

    [SetUp]
    public void BeforeEachTest()
    {
        Client = _webHostFactory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _webHostFactory.Dispose();
    }

    [TearDown]
    public void Teardown()
    {
        Client.Dispose(); // Dispose HttpClient
    }

    /// <summary>
    /// Resolves a service of type <typeparamref name="T"/> from the web host's service provider.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve.</typeparam>
    /// <returns>The resolved service of type <typeparamref name="T"/>.</returns>
    protected T ResolveService<T>() where T : notnull
    {
        try
        {
            if (_webHostFactory == null)
            {
                throw new IntegrationTestException("WebHostFactory is not initialized.");
            }

            var service = _webHostFactory.ResolveService<T>() ?? throw new IntegrationTestException($"Service of type {typeof(T).Name} could not be resolved.");
            return service;
        }
        catch (InvalidOperationException ex)
        {
            throw new IntegrationTestException("An error occurred while resolving the service. Make sure the service is registered in the container, Program.cs).", ex);
        }
    }
}