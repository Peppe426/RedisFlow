namespace TestBase.Contracts;

public interface ITestExecutionSettings
{
    string Language { get; set; }
    TimeZoneInfo TimeZone { get; set; }
    DateTime UtcDateTime { get; set; }

    static abstract void Initialize();

    DateTime GetLocalTime();

    void SetTimeZone(string timeZoneId);
}