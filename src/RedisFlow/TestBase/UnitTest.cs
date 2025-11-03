using TestBase.Core;

namespace TestBase;

[TestFixture]
[Category("Unit test")]
[Parallelizable(ParallelScope.All)]
public class UnitTest : TestExecutionSettings
{
    [OneTimeSetUp]
    public void OnetimeSetup()
    {
        // Runs once before any tests in this class
    }

    [SetUp]
    public void TestSetup()
    {
        // Runs before each test
    }

    [TearDown]
    public void TestTeardown()
    {
        // Runs after each test
    }

    [OneTimeTearDown]
    public void OnetimeTeardown()
    {
        // Runs once after all tests in this class
    }
}