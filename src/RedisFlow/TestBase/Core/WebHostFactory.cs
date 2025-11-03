using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace TestBase.Core;

public class WebHostFactory<TEntryPoint, TContext> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
    where TContext : DbContext // Allow TContext to be null
{
    private readonly SqliteConnection? _connection;
    private IServiceScope? _serviceScope;
    private IServiceProvider? _serviceProvider;
    private TContext? _context;

    private readonly Action<IServiceCollection>? _configureServices;

    public TContext? Context => _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebHostFactory{TEntryPoint, TContext}"/> class.
    /// </summary>
    /// <param name="configureServices">An optional action to configure additional services.</param>
    public WebHostFactory(Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
        SQLitePCL.Batteries.Init();

        // Only initialize the connection if TContext is not null
        if (typeof(TContext) != typeof(DbContext) && typeof(TContext) != null)
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
        }
    }

    /// <summary>
    /// Configures the web host.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Additional configuration can be added here
        });

        builder.ConfigureServices(services =>
        {
            // Always initialize the service provider and scope
            InitServiceProvider(services);

            // Initialize the in-memory database if TContext is provided
            InitDatabase(services);

            _configureServices?.Invoke(services);
        });
    }

    /// <summary>
    /// Initializes the service provider and creates a new scope for resolving services.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    private void InitServiceProvider(IServiceCollection serviceCollection)
    {
        // Build the service provider and create a new scope for resolving services
        _serviceProvider = serviceCollection.BuildServiceProvider();
        _serviceScope = _serviceProvider.CreateScope();
    }

    /// <summary>
    /// Initializes the in-memory database if a connection is provided.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    private void InitDatabase(IServiceCollection serviceCollection)
    {
        if (_connection != null)
        {
            serviceCollection.AddDbContext<TContext>((container, options) =>
            {
                options.UseSqlite(_connection);
            });

            _context = _serviceScope!.ServiceProvider.GetService<TContext>();

            if (_context != null)
            {
                _context.Database.EnsureDeleted();
                _context.Database.EnsureCreated();
            }
        }
    }

    /// <summary>
    /// Resolves a service of type <typeparamref name="T"/> from the service provider.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve.</typeparam>
    /// <returns>The resolved service of type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service scope is not initialized.</exception>
    public T? ResolveService<T>() where T : notnull
    {
        if (_serviceScope == null)
        {
            throw new InvalidOperationException("Service scope is not initialized. Ensure that InitServiceProvider has been called.");
        }

        return _serviceScope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="WebHostFactory{TEntryPoint, TContext}"/>.
    /// </summary>
    /// <param name="disposing">A boolean value indicating whether the method is being called from the Dispose method.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            (_context as IDisposable)?.Dispose();
            _serviceScope?.Dispose();
            _connection?.Dispose();
            base.Dispose(disposing);
        }
    }
}
