using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: CSharpStyleFormatter <file-or-directory> [more paths...]");
    return 1;
}

var targets = CollectTargets(args).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
if (targets.Length == 0)
{
    Console.Error.WriteLine("No .cs files found.");
    return 1;
}

var changedCount = 0;
var skippedCount = 0;
var parseOptions = new CSharpParseOptions(languageVersion: LanguageVersion.Preview, documentationMode: DocumentationMode.Parse);
foreach (var filePath in targets)
{
    var (fileText, encoding) = ReadAllText(filePath);
    var originalRoot = CSharpSyntaxTree.ParseText(fileText, parseOptions, path: filePath).GetRoot();
    var normalizedText = NormalizeWhitespace(originalRoot, fileText);
    if (normalizedText == fileText)
    {
        continue;
    }

    var normalizedRoot = CSharpSyntaxTree.ParseText(normalizedText, parseOptions, path: filePath).GetRoot();
    if (HasSyntaxErrors(normalizedRoot) && !HasSyntaxErrors(originalRoot))
    {
        Console.Error.WriteLine($"Skipped due to parse errors: {filePath}");
        skippedCount++;
        continue;
    }

    File.WriteAllText(filePath, normalizedText, encoding);
    changedCount++;
}

Console.WriteLine($"Formatted {changedCount} file(s). Skipped {skippedCount} file(s).");
return 0;

static IEnumerable<string> CollectTargets(IEnumerable<string> paths)
{
    foreach (var path in paths)
    {
        if (File.Exists(path))
        {
            if (string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
                yield return Path.GetFullPath(path);
            continue;
        }
        if (!Directory.Exists(path))
            continue;

        foreach (var filePath in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
        {
            var relativeParts = Path.GetRelativePath(path, filePath).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (relativeParts.Any(static part => part is "bin" or "obj" or "artifacts"))
                continue;
            yield return Path.GetFullPath(filePath);
        }
    }
}

static (string Text, Encoding Encoding) ReadAllText(string filePath)
{
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
    var text = reader.ReadToEnd();
    return (text, reader.CurrentEncoding);
}

static bool HasSyntaxErrors(SyntaxNode root) =>
    root.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

static string NormalizeWhitespace(SyntaxNode root, string originalText)
{
    var tokens = root.DescendantTokens(descendIntoTrivia: false).ToArray();
    if (tokens.Length == 0)
        return originalText;

    var builder = new StringBuilder(originalText.Length);
    builder.Append(tokens[0].LeadingTrivia.ToFullString());
    builder.Append(tokens[0].Text);
    for (var i = 1; i < tokens.Length; i++)
    {
        var previous = tokens[i - 1];
        var current = tokens[i];
        var betweenTrivia = previous.TrailingTrivia.AddRange(current.LeadingTrivia);
        builder.Append(FormatBetween(previous, current, betweenTrivia));
        builder.Append(current.Text);
    }
    builder.Append(tokens[^1].TrailingTrivia.ToFullString());
    return TrimTrailingWhitespace(builder.ToString());
}

static string FormatBetween(SyntaxToken previous, SyntaxToken current, SyntaxTriviaList betweenTrivia)
{
    if (ContainsProtectedTrivia(betweenTrivia))
        return betweenTrivia.ToFullString();
    if (ContainsEndOfLine(betweenTrivia))
    {
        if (current.IsKind(SyntaxKind.OpenBraceToken) && !previous.IsKind(SyntaxKind.SemicolonToken))
            return string.Empty;
        return betweenTrivia.ToFullString();
    }
    if (ShouldCompact(previous, current))
        return string.Empty;
    return betweenTrivia.ToFullString();
}

static bool ContainsProtectedTrivia(SyntaxTriviaList triviaList) =>
    triviaList.Any(static trivia => trivia.Kind() is not SyntaxKind.WhitespaceTrivia and not SyntaxKind.EndOfLineTrivia);

static bool ContainsEndOfLine(SyntaxTriviaList triviaList) =>
    triviaList.Any(static trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));

static bool ShouldCompact(SyntaxToken previous, SyntaxToken current) =>
    current.IsKind(SyntaxKind.OpenParenToken)
    || previous.IsKind(SyntaxKind.OpenParenToken)
    || current.IsKind(SyntaxKind.CloseParenToken)
    || current.IsKind(SyntaxKind.OpenBracketToken)
    || previous.IsKind(SyntaxKind.OpenBracketToken)
    || current.IsKind(SyntaxKind.CloseBracketToken)
    || previous.IsKind(SyntaxKind.OpenBraceToken)
    || current.IsKind(SyntaxKind.CloseBraceToken)
    || current.IsKind(SyntaxKind.OpenBraceToken)
    || previous.IsKind(SyntaxKind.DotToken)
    || current.IsKind(SyntaxKind.DotToken)
    || previous.IsKind(SyntaxKind.CommaToken)
    || current.IsKind(SyntaxKind.CommaToken)
    || previous.IsKind(SyntaxKind.ColonToken)
    || current.IsKind(SyntaxKind.ColonToken)
    || current.IsKind(SyntaxKind.SemicolonToken)
    || previous.IsKind(SyntaxKind.LessThanToken)
    || current.IsKind(SyntaxKind.LessThanToken)
    || current.IsKind(SyntaxKind.GreaterThanToken)
    || ShouldCompactAfterGreaterThan(previous)
    || current.IsKind(SyntaxKind.QuestionToken)
    || ShouldCompactAfterQuestion(previous)
    || current.IsKind(SyntaxKind.ExclamationToken)
    || ShouldCompactAfterExclamation(previous)
    || IsCompactOperator(previous)
    || IsCompactOperator(current);

static bool ShouldCompactAfterGreaterThan(SyntaxToken token) =>
    token.IsKind(SyntaxKind.GreaterThanToken)
    && token.Parent is not TypeArgumentListSyntax
    && token.Parent is not TypeParameterListSyntax;

static bool ShouldCompactAfterQuestion(SyntaxToken token) =>
    token.IsKind(SyntaxKind.QuestionToken)
    && token.Parent is not NullableTypeSyntax;

static bool ShouldCompactAfterExclamation(SyntaxToken token) =>
    token.IsKind(SyntaxKind.ExclamationToken);

static bool IsCompactOperator(SyntaxToken token) => token.Kind() is
    SyntaxKind.EqualsToken
    or SyntaxKind.PlusToken
    or SyntaxKind.MinusToken
    or SyntaxKind.AsteriskToken
    or SyntaxKind.SlashToken
    or SyntaxKind.PercentToken
    or SyntaxKind.AmpersandToken
    or SyntaxKind.BarToken
    or SyntaxKind.CaretToken
    or SyntaxKind.PlusPlusToken
    or SyntaxKind.MinusMinusToken
    or SyntaxKind.EqualsEqualsToken
    or SyntaxKind.ExclamationEqualsToken
    or SyntaxKind.LessThanEqualsToken
    or SyntaxKind.GreaterThanEqualsToken
    or SyntaxKind.AmpersandAmpersandToken
    or SyntaxKind.BarBarToken
    or SyntaxKind.QuestionQuestionToken
    or SyntaxKind.EqualsGreaterThanToken
    or SyntaxKind.MinusGreaterThanToken
    or SyntaxKind.PlusEqualsToken
    or SyntaxKind.MinusEqualsToken
    or SyntaxKind.AsteriskEqualsToken
    or SyntaxKind.SlashEqualsToken
    or SyntaxKind.PercentEqualsToken
    or SyntaxKind.AmpersandEqualsToken
    or SyntaxKind.BarEqualsToken
    or SyntaxKind.CaretEqualsToken
    or SyntaxKind.LessThanLessThanToken
    or SyntaxKind.GreaterThanGreaterThanToken;

static string TrimTrailingWhitespace(string text) =>
    Regex.Replace(text, @"[ \t]+(?=\r?$)", string.Empty, RegexOptions.Multiline);