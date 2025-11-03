using Microsoft.EntityFrameworkCore;
using TestBase.Core;

namespace TestBase;

[TestFixture]
[Category("Integration test")]
[Parallelizable(ParallelScope.None)]
public class IntegrationTest<TEntryPoint, TContext> : WebApplicationFactoryIntegrationTestBase<TEntryPoint, TContext> where TEntryPoint : class where TContext : DbContext
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