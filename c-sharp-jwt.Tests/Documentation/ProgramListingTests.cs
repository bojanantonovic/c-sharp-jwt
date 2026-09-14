using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace c_sharp_jwt.Tests.Documentation;

/// <summary>
/// PROGRAM.md quotes Program.cs with line numbers and refers to those numbers throughout its prose. Nothing in
/// the toolchain stops the two from drifting apart once someone edits the source, so the build checks it.
/// </summary>
public class ProgramListingTests
{
    private const string SolutionFileName = "c-sharp-jwt.sln";
    private const string MarkdownFileName = "PROGRAM.md";
    private const string ProjectDirectoryName = "c-sharp-jwt";
    private const string ProgramFileName = "Program.cs";

    /// <summary>Matches the first fenced C# block, whose content is the quoted listing.</summary>
    private const string ListingPattern = "```csharp\n(.*?)```";

    /// <summary>A listing line is its number, then two spaces and the source line, which may be absent when empty.</summary>
    private const string ListingLinePattern = @"^\s*(\d+)(?:  (.*))?$";

    private const string RepositoryRootNotFoundMessage = $"No directory above the test assembly contains {SolutionFileName}";

    [Fact]
    public void GivenTheListingInProgramMarkdown_WhenComparedToTheSource_ThenItQuotesEveryLineVerbatim()
    {
        // given
        var source = ReadSourceLines();

        // when
        var listing = ReadListing();

        // then
        Assert.Equal(source, listing.Select(line => line.Content));
    }

    [Fact]
    public void GivenTheListingInProgramMarkdown_WhenReadingItsLineNumbers_ThenTheyRunFromOneWithoutAGap()
    {
        // given
        var listing = ReadListing();

        // when
        var numbers = listing.Select(line => line.Number);

        // then
        Assert.Equal(Enumerable.Range(1, listing.Length), numbers);
    }

    private static (int Number, string Content)[] ReadListing()
    {
        var markdown = File.ReadAllText(Path.Combine(RepositoryRoot(), MarkdownFileName));
        var listing = Regex.Match(markdown, ListingPattern, RegexOptions.Singleline);
        Assert.True(listing.Success, $"{MarkdownFileName} contains no C# listing");

        return SplitLines(listing.Groups[1].Value).Select(ParseListingLine).ToArray();
    }

    private static (int Number, string Content) ParseListingLine(string line)
    {
        var parsed = Regex.Match(line, ListingLinePattern);
        Assert.True(parsed.Success, $"Unexpected listing line in {MarkdownFileName}: '{line}'");

        return (int.Parse(parsed.Groups[1].Value), parsed.Groups[2].Value);
    }

    private static string[] ReadSourceLines() =>
        SplitLines(File.ReadAllText(Path.Combine(RepositoryRoot(), ProjectDirectoryName, ProgramFileName)));

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException(RepositoryRootNotFoundMessage);
    }
}
