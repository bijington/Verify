namespace VerifyExpecto;

// ReSharper disable RedundantSuppressNullableWarningExpression

public static partial class Verifier
{
    public static Task<VerifyResult> VerifyJson(
        string name,
        string? target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "")
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, name, _ => _.VerifyJson(target));
    }

    public static Task<VerifyResult> VerifyJson(
        string name,
        Stream? target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "")
    {
        var assembly = Assembly.GetCallingAssembly()!;
        return Verify(settings, assembly, sourceFile, name, _ => _.VerifyJson(target));
    }
}