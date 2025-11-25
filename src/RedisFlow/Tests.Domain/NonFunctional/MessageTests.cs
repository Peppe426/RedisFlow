using System.Text;
using System.Xml.Serialization;
using Bogus;
using FluentAssertions;
using RedisFlow.Domain.Extensions;
using RedisFlow.Domain.ValueObjects;

namespace Tests.Domain.NonFunctional;

[TestFixture]
[Category("NonFunctional")]
public class MessageTests
{
    [Test]
    [Category("Performance")]
    public void Should_DemonstrateProtobufEfficiency_WhenComparingSerializationFormats()
    {
        // Given - Realistic business message with substantial data
        var producer = "order-processing-service@v3.2.1-production-eu-west-1";
        // Generate a realistic ECG / patient registry-like message using a reusable helper
        var content = GenerateEcgContent();

        var timestamp = new DateTimeOffset(2025, 11, 24, 15, 30, 45, 123, TimeSpan.Zero);
        var message = new Message<string>(producer, content) { CreatedAt = timestamp };

        // When - Serialize using Protobuf
        var protobufBytes = message.ToBytes();

        // When - Serialize using JSON (System.Text.Json)
        var jsonRepresentation = System.Text.Json.JsonSerializer.Serialize(message);
        var jsonBytes = Encoding.UTF8.GetBytes(jsonRepresentation);

        // When - Serialize using XML
        var xmlSerializer = new XmlSerializer(typeof(Message<string>));
        byte[] xmlBytes;
        using (var memoryStream = new System.IO.MemoryStream())
        {
            xmlSerializer.Serialize(memoryStream, message);
            xmlBytes = memoryStream.ToArray();
        }

        // Then - Calculate size comparisons
        var protobufSize = protobufBytes.Length;
        var jsonSize = jsonBytes.Length;
        var xmlSize = xmlBytes.Length;

        var jsonSavingsBytes = jsonSize - protobufSize;
        var jsonSavingsPercent = (jsonSavingsBytes * 100.0 / jsonSize);

        var xmlSavingsBytes = xmlSize - protobufSize;
        var xmlSavingsPercent = (xmlSavingsBytes * 100.0 / xmlSize);

        // Then - Log detailed comparison
        TestContext.Out.WriteLine("=== Serialization Format Comparison (Non-Functional Test) ===");
        TestContext.Out.WriteLine($"Message Producer: {producer}");
        TestContext.Out.WriteLine($"Content Length: {content.Length} characters");
        TestContext.Out.WriteLine("");
        TestContext.Out.WriteLine("--- Size Measurements ---");
        TestContext.Out.WriteLine($"Protobuf: {protobufSize:N0} bytes");
        TestContext.Out.WriteLine($"JSON:     {jsonSize:N0} bytes");
        TestContext.Out.WriteLine($"XML:      {xmlSize:N0} bytes");
        TestContext.Out.WriteLine("");
        TestContext.Out.WriteLine("--- Size Savings vs Protobuf ---");
        TestContext.Out.WriteLine($"vs JSON: {jsonSavingsBytes:N0} bytes ({jsonSavingsPercent:F1}% reduction)");
        TestContext.Out.WriteLine($"vs XML:  {xmlSavingsBytes:N0} bytes ({xmlSavingsPercent:F1}% reduction)");

        // Then - Assert Protobuf efficiency
        protobufSize.Should().BeLessThan(jsonSize,
            "because protobuf binary format should be more compact than JSON text format");
        protobufSize.Should().BeLessThan(xmlSize,
            "because protobuf binary format should be more compact than XML text format");

        jsonSavingsPercent.Should().BeGreaterThan(0,
            "because protobuf should provide measurable size reduction over JSON");
        xmlSavingsPercent.Should().BeGreaterThan(0,
            "because protobuf should provide measurable size reduction over XML");
    }

    private static string GenerateEcgContent(int? seed = null)
    {
        if (seed.HasValue)
        {
            Randomizer.Seed = new Random(seed.Value);
        }

        var ecgFaker = new Faker<string>().CustomInstantiator(f => $@"ECG Data Processing & Patient Registry Entry Complete | RecordID: ECG-{f.Date.Recent(30):yyyy-MM-dd}-MED-{f.Random.Number(100000, 999999)} | Patient: {f.Name.FullName()} (PatientID: PAT-{f.Random.Number(200000, 999999)}) | Date of Birth: {f.Date.Past(80, DateTime.Today.AddYears(-18)):yyyy-MM-dd} | Gender: {f.Person.Gender} |
            Study Details: [
                {{ExamID: ECG-{f.Random.AlphaNumeric(6).ToUpper()}-2025, Type: '12-Lead Resting ECG', DateTime: '{f.Date.Recent(10):yyyy-MM-dd HH:mm:ss}', Duration: '10 seconds', SamplingRate: 500 Hz, Device: '{f.PickRandom("CardioMax-12", "GE MAC 5500", "Philips PageWriter")}'}}
                    ] |
                Interpretation Summary: {f.PickRandom("Sinus rhythm", "Atrial fibrillation", "Sinus bradycardia")} with rate {f.Random.Int(38, 160)} bpm | QTc {f.Random.Int(360, 520)} ms |
                Automated Diagnosis: {f.PickRandom("Normal ECG", "Abnormal ECG", "Borderline")} | Status: {f.PickRandom("Final", "Preliminary", "Pending overread")} |
            Notes: {f.Lorem.Sentence()}");

        return ecgFaker.Generate();
    }
}