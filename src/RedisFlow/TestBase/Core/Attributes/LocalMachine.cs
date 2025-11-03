namespace TestBase.Core.Attributes;

public static class LocalMachine
{
    public static void IsTrue()
    {
        bool isDebugMode = System.Diagnostics.Debugger.IsAttached;

        IEnumerable<object> categories = TestContext.CurrentContext.Test.Properties["Category"];
        bool isLocalOnlyTest = categories.Any(cat => cat.ToString() == "LocalOnlyTest");

        if (isLocalOnlyTest && !isDebugMode)
        {
            Assert.Ignore("Skipping local-only test on non-local machine.");
        }
    }
}