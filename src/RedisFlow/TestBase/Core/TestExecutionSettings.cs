using System.Runtime.CompilerServices;
using TestBase.Contracts;

namespace TestBase.Core;

public abstract class TestExecutionSettings : ITestExecutionSettings
{
    private DateTime _utcDateTime;
    private string _language;
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;
    internal string _testResultLogFilePath = string.Empty;
    internal string _approvalMessage = string.Empty;
    internal static readonly SemaphoreSlim _logFileSemaphore = new(1, 1);

    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyImageHash.Initialize();
    }

    protected TestExecutionSettings(string language = "sv", string timeZoneId = "UTC")
    {
        _utcDateTime = DateTime.UtcNow;
        _language = language;
        SetTimeZone(timeZoneId);
    }

    public DateTime UtcDateTime
    {
        get => _utcDateTime;
        set
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("DateTime must be in UTC.", nameof(value));
            }
            _utcDateTime = value;
        }
    }

    public string Language
    {
        get => _language;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Language cannot be null or empty.");
            }
            _language = value;
        }
    }

    public TimeZoneInfo TimeZone
    {
        get => _timeZone;
        set => _timeZone = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void SetTimeZone(string timeZoneId)
    {
        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback to UTC if the time zone is not found
            _timeZone = TimeZoneInfo.Utc;
        }
        catch (ArgumentNullException)
        {
            _timeZone = TimeZoneInfo.Utc;
        }
    }

    public DateTime GetLocalTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(UtcDateTime, _timeZone);
    }

    protected string PrepareTestOutcomeDirectory(string project = "TestBase", string directory = "TestOutcomes")
    {
        static string GetSolutionDirectory()
        {
            DirectoryInfo? dir = new(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && dir.GetFiles("*.sln").Length == 0)
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        var testOutcomesFolder = Path.Combine(GetSolutionDirectory(), project, directory);
        Directory.CreateDirectory(testOutcomesFolder);

        // Append datetime to the file name
        var output = InitializeTestResultLogFile(testOutcomesFolder);
        return output;
    }

    protected static VerifySettings BuildDefaultVerifySettings()
    {
        var settings = new VerifySettings();
        settings.ScrubInlineGuids();
        settings.ScrubInlineDates("yyyy-MM-dd");
        return settings;
    }

    private string InitializeTestResultLogFile(string testOutcomesFolder)
    {
        var timestamp = GetLocalTime().ToString("yyyy-MM-dd");
        var output = Path.Combine(testOutcomesFolder, $"{timestamp}.md");
        _testResultLogFilePath = output;

        // Write markdown header if file is new or empty
        if (!File.Exists(_testResultLogFilePath) || new FileInfo(_testResultLogFilePath).Length == 0)
        {
            var header = "| Timestamp | Test Name | Outcome | Requirement | Message |\n|---|---|---|---|---|\n";
            File.AppendAllText(_testResultLogFilePath, header);
        }

        return output;
    }

    internal async Task RecordTestResult()
    {
        var testName = TestContext.CurrentContext.Test.Name;
        var outcome = TestContext.CurrentContext.Result.Outcome.Status;
        var categories = TestContext.CurrentContext.Test.Properties["Category"];

        // Collect all custom properties except Category and Description
        var customProperties = TestContext.CurrentContext.Test.Properties
            .Keys
            .Cast<string>()
            .Where(k => k != "Category" && k != "Description")
            .Select(k => $"{k}:{string.Join(",", TestContext.CurrentContext.Test.Properties[k].Cast<object>())}");
        var requirementString = string.Join(", ", customProperties);

        var logEntry = $"{DateTime.UtcNow:yyyy-MM-dd} | {testName} | {outcome} | {requirementString} | {_approvalMessage}";

        await _logFileSemaphore.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_testResultLogFilePath, logEntry + Environment.NewLine);
        }
        finally
        {
            _logFileSemaphore.Release();
        }
    }
}