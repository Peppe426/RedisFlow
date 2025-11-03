E
# 🧪 Test Structure

Structure for writing tests using **NUnit** and **FluentAssertions**.
All tests should follow the **Given / When / Then** pattern for clarity.

## ✅ General Rules

1. **Structure tests** as:

   ```csharp
   // Given
   // When
   // Then
   ```
2. **Naming convention**:
   `Should_[ExpectedBehavior]_When_[Condition]`
3. **Use FluentAssertions** for readable, expressive assertions.
4. **Use `Action` for void methods**, **`Func<T>` for returning values**, and **`Func<Task>` for async methods**.
5. **Defer execution** when testing for exceptions.
6. **Keep one logical assertion per test** 

---

## 🧩 1️⃣ Void Method (Side Effects)

```csharp
[Test]
public void Should_UpdateState_When_Called()
{
    // Given
    var sut = new MyService();

    // When
    sut.UpdateState(); // returns void

    // Then
    sut.State.Should().Be("Updated", "because calling UpdateState should change the internal state");
}
```

---

## 🧩 2️⃣ Returning Expected Value

```csharp
[Test]
public void Should_ReturnExpectedValue_When_ConditionIsMet()
{
    // Given
    var sut = new MyService();
    var expected = "expected result";

    // When
    var outcome = sut.GetValue(); // returns string

    // Then
    outcome.Should().Be(expected, "because the condition should produce the expected result");
}
```

---

## 🧩 3️⃣ Handling Exceptions

```csharp
[Test]
public void Should_ThrowInvalidOperationException_When_InvalidState()
{
    // Given
    var sut = new MyService();

    // When
    Action act = () => sut.DoSomethingInvalid(); // void method expected to throw

    // Then
    act.Should()
       .Throw<InvalidOperationException>()
       .WithMessage("Specific reason for failure")
       .Because("invalid state should trigger this exception");
}
```

---

## ⚙️ 4️⃣ Async Method (Returning Value)

```csharp
[Test]
public async Task Should_ReturnExpectedResult_When_AsyncOperationCompletes()
{
    // Given
    var sut = new MyAsyncService();
    var expected = "done";

    // When
    var outcome = await sut.GetValueAsync();

    // Then
    outcome.Should().Be(expected, "because the async operation should produce the expected result");
}
```

---

## ⚙️ 5️⃣ Async Exception Handling

```csharp
[Test]
public async Task Should_ThrowInvalidOperationException_When_AsyncOperationFails()
{
    // Given
    var sut = new MyAsyncService();

    // When
    Func<Task> act = async () => await sut.DoSomethingInvalidAsync();

    // Then
    await act.Should()
             .ThrowAsync<InvalidOperationException>()
             .WithMessage("Specific reason for failure");

            //  .Because("invalid async state should trigger this exception"); not working here
}
```

## 🧭 Naming Guidelines

Follow this pattern for consistent, readable test names:

```
Should_[ExpectedBehavior]_When_[Condition]
```

Examples:

* `Should_UpdateState_When_Called`
* `Should_ReturnExpectedValue_When_ConditionIsMet`
* `Should_ThrowInvalidOperationException_When_InvalidState`

Keep names **explicit and intention-revealing** — tests should describe *what* and *when*, not *how*.

---

## 🧰 Base Template Snippet

```csharp
[TestFixture]
public class MyServiceTests
{
    [Test]
    public void Should_DoSomething_When_Condition()
    {
        // Given
        var sut = new MyService();
        var expected = "expected result";

        // When
        var outcome = sut.DoSomething();

        // Then
        outcome.Should().Be(expected, "because this is the expected behavior");
    }
}
```

### Async Variant

```csharp
[TestFixture]
public class MyAsyncServiceTests
{
    [Test]
    public async Task Should_DoSomethingAsync_When_Condition()
    {
        // Given
        var sut = new MyAsyncService();
        var expected = "expected result";

        // When
        var outcome = await sut.DoSomethingAsync();

        // Then
        outcome.Should().Be(expected, "because this is the expected behavior");
    }
}
```
---

## ⚖️ Do / Don’t Guidelines

### ✅ **Do**

* **Do use the Given / When / Then structure** for all tests.
* **Do name tests clearly**: `Should_[ExpectedBehavior]_When_[Condition]`.
* **Do use FluentAssertions** for readable, expressive assertions.
* **Do use `Action` or `Func<Task>` for deferred execution** when testing exceptions.
* **Do keep one logical assertion per test** — split tests for separate concerns.
* **Do assert exception messages** when they are meaningful.
* **Do test both sync and async methods consistently**.
* **Do use meaningful variable names**: `outcome` for results, `act` for deferred actions.

### ❌ **Don’t**

* **Don’t mix setup, execution, and assertions** in the same phase.
* **Don’t call a method directly in the “Then” phase** if testing for exceptions — wrap in `Action` or `Func<Task>`.
* **Don’t assert multiple unrelated things in a single test** — makes failures harder to debug.
* **Don’t ignore async exceptions** — always use `ThrowAsync<T>()`.
* **Don’t use vague names** like `result1`, `test1` — always make intentions clear.
* **Don’t suppress exceptions manually** — let FluentAssertions handle it for clear messages.

---