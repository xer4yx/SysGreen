using System.IO;
using System.Text.RegularExpressions;

namespace SysGreen.App.Services;

/// <summary>
/// Supplies the Privacy Policy &amp; Terms text and its version (ADR-0018). The version is parsed from
/// the document's own <c>**Policy version:** N</c> header, so the text and the gate stay in sync —
/// bump the number in <c>policy.md</c> and the acceptance gate re-prompts.
/// </summary>
public interface IPolicyProvider
{
    int CurrentVersion { get; }
    string Text { get; }
}

public sealed class FilePolicyProvider : IPolicyProvider
{
    private readonly string _path;

    public FilePolicyProvider(string path) => _path = path;

    public string Text => File.ReadAllText(_path);

    public int CurrentVersion => ParseVersion(Text);

    /// <summary>Reads the <c>Policy version: N</c> marker from the document. 0 when absent.</summary>
    public static int ParseVersion(string policyText)
    {
        var match = Regex.Match(policyText, @"Policy version:\**\s*(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}
