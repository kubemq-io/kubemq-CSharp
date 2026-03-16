namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Defines the tested server compatibility range for client-server version validation.
/// </summary>
internal static class CompatibilityConstants
{
    // Use exactly 3 components (MAJOR.MINOR.PATCH) for consistency.
    // System.Version treats 3-part and 4-part versions differently in comparisons:
    // new Version(3, 0, 0) < new Version(3, 0, 0, 0) returns true in .NET,
    // which is counterintuitive. Always use 3-part versions here and in
    // NormalizeVersion() to avoid this edge case.
    internal const string MinTestedServerVersion = "3.0.0";
    internal const string MaxTestedServerVersion = ""; // empty = no upper bound
    internal const string CompatibilityMatrixUrl =
        "https://github.com/kubemq-io/kubemq-CSharp/blob/HEAD/COMPATIBILITY.md";
}
