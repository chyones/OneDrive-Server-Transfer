using System.Reflection;
using OneDriveServerTransfer.Destination;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// Every user-facing destination error carries the contract-required shape (title,
/// plain explanation, corrective action, stable reference code), and reference codes
/// are unique and never renumbered accidentally.
/// </summary>
public class DestinationErrorsTests
{
    public static TheoryData<DestinationException> AllErrors => new(
        typeof(DestinationErrors)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(DestinationException))
            .Select(method => (DestinationException)method.Invoke(null, [null])!));

    [Theory]
    [MemberData(nameof(AllErrors))]
    public void EveryErrorHasTheUserFacingShape(DestinationException error)
    {
        Assert.False(string.IsNullOrWhiteSpace(error.ReferenceCode));
        Assert.StartsWith("DST-", error.ReferenceCode, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(error.Title));
        Assert.False(string.IsNullOrWhiteSpace(error.Explanation));
        Assert.False(string.IsNullOrWhiteSpace(error.CorrectiveAction));
    }

    [Fact]
    public void ReferenceCodesAreUnique()
    {
        var codes = typeof(DestinationErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetValue(null)!)
            .ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.StartsWith("DST-", code, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryErrorCodeHasAFactoryAndEveryFactoryUsesADefinedCode()
    {
        var defined = typeof(DestinationErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetValue(null)!)
            .ToHashSet(StringComparer.Ordinal);

        var used = typeof(DestinationErrors)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(DestinationException))
            .Select(method => ((DestinationException)method.Invoke(null, [null])!).ReferenceCode)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(defined.SetEquals(used), "Every defined code must have a factory and vice versa.");
    }
}
