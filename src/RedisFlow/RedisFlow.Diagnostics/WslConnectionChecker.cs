using StackExchange.Redis;
using System.Diagnostics;
using Spectre.Console;

namespace RedisFlow.Diagnostics;

/// <summary>
/// Utility to diagnose and check Redis connection via WSL
/// </summary>
public class WslConnectionChecker
{
    private const int RedisPort = 6379;

    public static async Task<bool> CheckConnectionAsync(string? connectionString = null)
    {
        AnsiConsole.Write(
            new FigletText("Redis WSL Check")
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[green]Starting Redis connection diagnostics...[/]\n");

        // Step 1: Check WSL availability
        AnsiConsole.MarkupLine("[yellow]Step 1:[/] Checking WSL availability...");
        var wslAvailable = await IsWslAvailableAsync();
        if (!wslAvailable)
        {
            AnsiConsole.MarkupLine("[red]WSL not available. Please ensure WSL 2 is installed.[/]");
            return false;
        }
        AnsiConsole.MarkupLine("[green]? WSL is available[/]\n");

        // Step 2: Check Redis service status
        AnsiConsole.MarkupLine("[yellow]Step 2:[/] Checking Redis service status in WSL...");
        var redisRunning = await IsRedisRunningAsync();
        if (!redisRunning)
        {
            AnsiConsole.MarkupLine("[yellow]? Redis service not running. Attempting to start...[/]");
            var started = await TryStartRedisAsync();
            if (!started)
            {
                AnsiConsole.MarkupLine("[red]? Failed to start Redis. Please start manually.[/]");
                return false;
            }
            AnsiConsole.MarkupLine("[green]? Redis started successfully[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]? Redis is running[/]\n");
        }

        // Step 3: Get WSL IP
        AnsiConsole.MarkupLine("[yellow]Step 3:[/] Getting WSL IP address...");
        var wslIp = await GetWslIpAsync();
        if (string.IsNullOrWhiteSpace(wslIp))
        {
            AnsiConsole.MarkupLine("[red]? Failed to get WSL IP address[/]");
            return false;
        }
        AnsiConsole.MarkupLine($"[green]? WSL IP: {wslIp}[/]\n");

        // Step 4: Test Redis CLI ping
        AnsiConsole.MarkupLine("[yellow]Step 4:[/] Testing redis-cli ping...");
        var cliPingSuccess = await TestRedisCliPingAsync();
        if (!cliPingSuccess)
        {
            AnsiConsole.MarkupLine("[red]? redis-cli ping failed[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]? redis-cli ping successful[/]\n");
        }

        // Step 5: Try connection from localhost
        AnsiConsole.MarkupLine("[yellow]Step 5:[/] Testing localhost connection...");
        var localhostConnection = await TestConnectionAsync("localhost:6379");
        if (localhostConnection)
        {
            AnsiConsole.MarkupLine($"[green]? Successfully connected to localhost:6379[/]\n");
            return true;
        }
        AnsiConsole.MarkupLine("[yellow]? localhost:6379 not reachable[/]");

        // Step 6: Try connection from WSL IP
        AnsiConsole.MarkupLine("[yellow]Step 6:[/] Testing WSL IP connection...");
        var wslIpEndpoint = $"{wslIp}:6379";
        var wslConnection = await TestConnectionAsync(wslIpEndpoint);
        if (wslConnection)
        {
            AnsiConsole.MarkupLine($"[green]? Successfully connected to {wslIpEndpoint}[/]\n");
            AnsiConsole.MarkupLine("[yellow]Note:[/] Use this endpoint for your configuration:");
            AnsiConsole.MarkupLine($"[cyan]{wslIpEndpoint}[/]\n");
            return true;
        }
        AnsiConsole.MarkupLine($"[red]? Could not connect to {wslIpEndpoint}[/]\n");

        // Step 7: Show troubleshooting
        ShowTroubleshootingSteps(wslIp);
        return false;
    }

    private static async Task<bool> IsWslAvailableAsync()
    {
        try
        {
            var result = await RunWslCommandAsync("--version");
            return !string.IsNullOrWhiteSpace(result);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsRedisRunningAsync()
    {
        var output = await RunWslCommandAsync("service redis-server status");
        return output.Contains("redis-server is running", StringComparison.OrdinalIgnoreCase)
            || output.Contains("active (running)", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> TryStartRedisAsync()
    {
        var hasSudo = (await RunWslCommandAsync("which sudo")).Trim().Length > 0;
        var cmd = hasSudo ? "sudo service redis-server start" : "service redis-server start";

        var result = await RunWslCommandAsync(cmd, captureError: true);
        
        // Give Redis time to start
        await Task.Delay(2000);
        
        return result.Contains("Starting") 
            || result.Contains("done", StringComparison.OrdinalIgnoreCase)
            || await IsRedisRunningAsync();
    }

    private static async Task<string?> GetWslIpAsync()
    {
        try
        {
            var output = await RunWslCommandAsync("hostname -I");
            var ip = output.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return ip;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> TestRedisCliPingAsync()
    {
        try
        {
            var output = await RunWslCommandAsync("redis-cli ping");
            return output.Contains("PONG", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestConnectionAsync(string endpoint)
    {
        try
        {
            var opts = new ConfigurationOptions
            {
                EndPoints = { endpoint },
                AbortOnConnectFail = false,
                ConnectRetry = 2,
                ConnectTimeout = 3000,
                SyncTimeout = 3000
            };

            using var mux = await ConnectionMultiplexer.ConnectAsync(opts);
            if (!mux.IsConnected)
                return false;

            var result = await mux.GetDatabase().PingAsync();
            return result.TotalMilliseconds >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunWslCommandAsync(string command, bool captureError = false)
    {
        try
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = captureError,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            var output = await p.StandardOutput.ReadToEndAsync();
            if (captureError)
                output += await p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync();
            return output;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static void ShowTroubleshootingSteps(string wslIp)
    {
        AnsiConsole.MarkupLine("[yellow]Troubleshooting Steps:[/]\n");

        var steps = new string[]
        {
            "1. Verify Redis is installed: wsl apt-get update && wsl apt-get install redis-server",
            "2. Check Redis service: wsl service redis-server status",
            "3. Start Redis manually: wsl sudo service redis-server start",
            "4. Check Redis is listening: wsl sudo netstat -tlnp | grep redis",
            $"5. Configure Redis to accept external connections:",
            $"   wsl sudo sed -i 's/^bind 127.0.0.1/bind 0.0.0.0/' /etc/redis/redis.conf",
            "6. Restart Redis: wsl sudo service redis-server restart",
            "7. Test connection: wsl redis-cli ping",
            $"8. If using WSL IP, verify it's reachable: {wslIp}:6379"
        };

        foreach (var step in steps)
        {
            AnsiConsole.MarkupLine($"[cyan]{step}[/]");
        }

        AnsiConsole.WriteLine();
    }
}
