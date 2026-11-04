using System.Reflection;

namespace QuoteApp.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

public sealed class AssertionException(string message) : Exception(message);

public static class Assert
{
    public static void Equal<T>(T expected, T actual, string? what = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"{what ?? "Equal"}: expected <{expected}> but was <{actual}>");
    }

    public static void True(bool condition, string what)
    {
        if (!condition) throw new AssertionException($"Expected true: {what}");
    }

    public static void Throws<TEx>(Action action) where TEx : Exception
    {
        try { action(); }
        catch (TEx) { return; }
        catch (Exception ex) { throw new AssertionException($"Expected {typeof(TEx).Name} but got {ex.GetType().Name}"); }
        throw new AssertionException($"Expected {typeof(TEx).Name} but nothing was thrown");
    }
}

public static class Runner
{
    public static int Main()
    {
        var tests = typeof(Runner).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<TestAttribute>() is not null))
            .OrderBy(m => m.DeclaringType!.Name).ThenBy(m => m.Name).ToList();

        int passed = 0, failed = 0;
        foreach (var t in tests)
        {
            var name = $"{t.DeclaringType!.Name}.{t.Name}";
            try { t.Invoke(null, null); Console.WriteLine($"PASS  {name}"); passed++; }
            catch (TargetInvocationException ex)
            {
                Console.WriteLine($"FAIL  {name}\n      {ex.InnerException?.Message}");
                failed++;
            }
        }
        Console.WriteLine($"\nTotal: {tests.Count}, Passed: {passed}, Failed: {failed}");
        return failed == 0 ? 0 : 1;
    }
}
