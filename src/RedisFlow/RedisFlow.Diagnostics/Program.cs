using StackExchange.Redis;
using Spectre.Console;

namespace RedisFlow.Diagnostics;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var connectionString = args.Length > 0 ? args[0] : "localhost:6379";
        var streamName = args.Length > 1 ? args[1] : "mystream";

        AnsiConsole.Write(
            new FigletText("RedisFlow Diagnostics")
                .Color(Color.Red));

        AnsiConsole.MarkupLine($"[cyan]Connection:[/] {connectionString}");
        AnsiConsole.MarkupLine($"[cyan]Stream:[/] {streamName}");
        AnsiConsole.WriteLine();

        try
        {
            await using var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            var db = connection.GetDatabase();

            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[green]What would you like to inspect?[/]")
                        .PageSize(10)
                        .AddChoices(
                            "📊 Stream Information",
                            "📝 View Stream Entries",
                            "👥 Consumer Groups",
                            "📋 Pending Messages",
                            "🔄 Real-time Monitor",
                            "🔍 List All Streams",
                            "❌ Exit"));

                AnsiConsole.Clear();

                switch (choice)
                {
                    case "📊 Stream Information":
                        await ShowStreamInformation(db, streamName);
                        break;
                    case "📝 View Stream Entries":
                        await ShowStreamEntries(db, streamName);
                        break;
                    case "👥 Consumer Groups":
                        await ShowConsumerGroups(db, streamName);
                        break;
                    case "📋 Pending Messages":
                        await ShowPendingMessages(db, streamName);
                        break;
                    case "🔄 Real-time Monitor":
                        await MonitorStream(db, streamName);
                        break;
                    case "🔍 List All Streams":
                        await ListAllStreams(connection);
                        break;
                    case "❌ Exit":
                        return 0;
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                Console.ReadKey(true);
                AnsiConsole.Clear();
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static async Task ShowStreamInformation(IDatabase db, string streamName)
    {
        AnsiConsole.MarkupLine($"[yellow]Stream Information: {streamName}[/]");
        AnsiConsole.WriteLine();

        try
        {
            var length = await db.StreamLengthAsync(streamName);
            var info = await db.ExecuteAsync("XINFO", "STREAM", streamName);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Property")
                .AddColumn("Value");

            table.AddRow("Stream Name", streamName);
            table.AddRow("Length", length.ToString());

            if (!info.IsNull && info.Resp2Type == ResultType.Array)
            {
                var results = (RedisResult[])info!;
                for (int i = 0; i < results.Length; i += 2)
                {
                    var key = results[i].ToString();
                    var value = results[i + 1].ToString();
                    if (key != null && value != null)
                    {
                        table.AddRow(key, value);
                    }
                }
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private static async Task ShowStreamEntries(IDatabase db, string streamName)
    {
        AnsiConsole.MarkupLine($"[yellow]Stream Entries: {streamName}[/]");
        AnsiConsole.WriteLine();

        var count = AnsiConsole.Prompt(
            new TextPrompt<int>("How many entries to display?")
                .DefaultValue(10)
                .ValidationErrorMessage("[red]Please enter a valid number[/]"));

        try
        {
            var entries = await db.StreamRangeAsync(streamName, "-", "+", count, Order.Descending);

            if (entries.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No entries found in stream[/]");
                return;
            }

            foreach (var entry in entries)
            {
                var panel = new Panel(FormatStreamEntry(entry))
                {
                    Header = new PanelHeader($"Entry ID: {entry.Id}"),
                    Border = BoxBorder.Rounded
                };
                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private static async Task ShowConsumerGroups(IDatabase db, string streamName)
    {
        AnsiConsole.MarkupLine($"[yellow]Consumer Groups: {streamName}[/]");
        AnsiConsole.WriteLine();

        try
        {
            var groups = await db.ExecuteAsync("XINFO", "GROUPS", streamName);

            if (groups.IsNull || groups.Resp2Type != ResultType.Array)
            {
                AnsiConsole.MarkupLine("[yellow]No consumer groups found[/]");
                return;
            }

            var groupsArray = (RedisResult[])groups!;
            if (groupsArray.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No consumer groups found[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Group Name")
                .AddColumn("Consumers")
                .AddColumn("Pending")
                .AddColumn("Last Delivered ID");

            foreach (var group in groupsArray)
            {
                if (group.Resp2Type == ResultType.Array)
                {
                    var groupInfo = (RedisResult[])group!;
                    var groupData = new Dictionary<string, string>();

                    for (int i = 0; i < groupInfo.Length; i += 2)
                    {
                        var key = groupInfo[i].ToString();
                        var value = groupInfo[i + 1].ToString();
                        if (key != null && value != null)
                        {
                            groupData[key] = value;
                        }
                    }

                    table.AddRow(
                        groupData.GetValueOrDefault("name", "N/A"),
                        groupData.GetValueOrDefault("consumers", "N/A"),
                        groupData.GetValueOrDefault("pending", "N/A"),
                        groupData.GetValueOrDefault("last-delivered-id", "N/A"));
                }
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private static async Task ShowPendingMessages(IDatabase db, string streamName)
    {
        AnsiConsole.MarkupLine($"[yellow]Pending Messages: {streamName}[/]");
        AnsiConsole.WriteLine();

        try
        {
            var groups = await db.ExecuteAsync("XINFO", "GROUPS", streamName);

            if (groups.IsNull || groups.Resp2Type != ResultType.Array)
            {
                AnsiConsole.MarkupLine("[yellow]No consumer groups found[/]");
                return;
            }

            var groupsArray = (RedisResult[])groups!;
            var groupNames = new List<string>();

            foreach (var group in groupsArray)
            {
                if (group.Resp2Type == ResultType.Array)
                {
                    var groupInfo = (RedisResult[])group!;
                    for (int i = 0; i < groupInfo.Length; i += 2)
                    {
                        if (groupInfo[i].ToString() == "name")
                        {
                            var name = groupInfo[i + 1].ToString();
                            if (name != null)
                            {
                                groupNames.Add(name);
                            }
                            break;
                        }
                    }
                }
            }

            if (groupNames.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No consumer groups found[/]");
                return;
            }

            var selectedGroup = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select consumer group:")
                    .AddChoices(groupNames));

            var pending = await db.ExecuteAsync("XPENDING", streamName, selectedGroup);

            if (pending.IsNull || pending.Resp2Type != ResultType.Array)
            {
                AnsiConsole.MarkupLine("[yellow]No pending messages[/]");
                return;
            }

            var pendingInfo = (RedisResult[])pending!;
            if (pendingInfo.Length < 4)
            {
                AnsiConsole.MarkupLine("[yellow]No pending messages[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Property")
                .AddColumn("Value");

            table.AddRow("Total Pending", pendingInfo[0].ToString() ?? "0");
            table.AddRow("Smallest ID", pendingInfo[1].ToString() ?? "N/A");
            table.AddRow("Greatest ID", pendingInfo[2].ToString() ?? "N/A");

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private static async Task MonitorStream(IDatabase db, string streamName)
    {
        AnsiConsole.MarkupLine("[yellow]Real-time Stream Monitor (Press Ctrl+C to stop)[/]");
        AnsiConsole.WriteLine();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await AnsiConsole.Live(new Table())
                .StartAsync(async ctx =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var table = new Table()
                            .Border(TableBorder.Rounded)
                            .AddColumn("Metric")
                            .AddColumn("Value");

                        var length = await db.StreamLengthAsync(streamName);
                        table.AddRow("Stream Length", length.ToString());
                        table.AddRow("Timestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                        try
                        {
                            var groups = await db.ExecuteAsync("XINFO", "GROUPS", streamName);
                            if (!groups.IsNull && groups.Resp2Type == ResultType.Array)
                            {
                                var groupsArray = (RedisResult[])groups!;
                                table.AddRow("Consumer Groups", groupsArray.Length.ToString());
                            }
                        }
                        catch
                        {
                            table.AddRow("Consumer Groups", "0");
                        }

                        ctx.UpdateTarget(table);

                        try
                        {
                            await Task.Delay(2000, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[green]Monitor stopped[/]");
        }
    }

    private static async Task ListAllStreams(IConnectionMultiplexer connection)
    {
        AnsiConsole.MarkupLine("[yellow]All Redis Streams[/]");
        AnsiConsole.WriteLine();

        try
        {
            var server = connection.GetServers().First();
            var keys = server.Keys(pattern: "*", pageSize: 1000);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Stream Key")
                .AddColumn("Length")
                .AddColumn("Type");

            var db = connection.GetDatabase();

            foreach (var key in keys)
            {
                var type = await db.KeyTypeAsync(key);
                if (type == RedisType.Stream)
                {
                    var length = await db.StreamLengthAsync(key);
                    table.AddRow(key.ToString(), length.ToString(), "Stream");
                }
            }

            if (table.Rows.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No streams found[/]");
                return;
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private static string FormatStreamEntry(StreamEntry entry)
    {
        var lines = new List<string>();

        foreach (var field in entry.Values)
        {
            lines.Add($"[cyan]{field.Name}:[/] {field.Value}");
        }

        return string.Join("\n", lines);
    }
}
