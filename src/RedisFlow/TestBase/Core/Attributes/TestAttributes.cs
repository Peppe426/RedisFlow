namespace TestBase.Core.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LocalOnlyTestAttribute : CategoryAttribute
{ }