using System;
using System.Collections.Generic;
using System.Windows.Documents;
using System.Windows.Media;
using ColorCode;
using ColorCode.Common;
using ColorCode.Parsing;

namespace TheMarkdownWeb.Rendering.Highlighting;

/// <summary>
/// Story 3.4 — turns code into a flat sequence of monospace WPF <see cref="Run"/>s, each colored by
/// the ColorCode token scope it falls inside (the github-light palette). It subclasses ColorCode's
/// <see cref="CodeColorizerBase"/> and is driven exactly as ColorCode's own UWP
/// <c>RichTextBlockFormatter</c> / HTML <c>HtmlFormatter</c> are: the public <see cref="GetRuns"/>
/// entry calls <c>languageParser.Parse(code, language, (parsed, scopes) =&gt; Write(parsed, scopes))</c>,
/// and the <see cref="Write"/> override walks the scope tree to emit the runs.
///
/// Net10 has no shipped WPF formatter, so this is the tiny one we own. There is no networking / AI /
/// webview here — ColorCode.Core is a pure managed tokenizer.
///
/// Totality (AC5): <see cref="Write"/> is defensive on offsets/lengths (clamped, never
/// <c>Substring</c>-throws). The scope→brush map is total (unknown scope → the default foreground).
/// The produced runs concatenate back to the verbatim source (gap text between scopes is emitted as
/// default-foreground runs), so the 3.3 verbatim-text contract holds.
/// </summary>
internal sealed class FlowDocumentCodeColorizer : CodeColorizerBase
{
    // ---- github-light palette (AC2), exact ARGB, alpha 0xFF. One FROZEN brush instance per color
    // so DistinctForegrounds counts by reference (no per-token allocation).
    private static readonly SolidColorBrush KeywordBrush = Freeze(0xCF, 0x22, 0x2E); // #cf222e
    private static readonly SolidColorBrush StringBrush = Freeze(0x0A, 0x30, 0x69); // #0a3069
    private static readonly SolidColorBrush CommentBrush = Freeze(0x59, 0x63, 0x6E); // #59636e
    private static readonly SolidColorBrush NumberBrush = Freeze(0x05, 0x50, 0xAE); // #0550ae
    private static readonly SolidColorBrush FunctionBrush = Freeze(0x82, 0x50, 0xDF); // #8250df
    private static readonly SolidColorBrush DefaultBrush = Freeze(0x1F, 0x23, 0x28); // #1f2328

    // ScopeName (string constant) → brush. Total: any scope not present here maps to DefaultBrush.
    private static readonly IReadOnlyDictionary<string, SolidColorBrush> ScopeBrushes =
        new Dictionary<string, SolidColorBrush>(StringComparer.Ordinal)
        {
            // keyword
            [ScopeName.Keyword] = KeywordBrush,
            [ScopeName.ControlKeyword] = KeywordBrush,
            [ScopeName.PreprocessorKeyword] = KeywordBrush,
            [ScopeName.PseudoKeyword] = KeywordBrush,
            // string
            [ScopeName.String] = StringBrush,
            [ScopeName.StringCSharpVerbatim] = StringBrush,
            [ScopeName.StringEscape] = StringBrush,
            [ScopeName.JsonString] = StringBrush,
            // comment
            [ScopeName.Comment] = CommentBrush,
            [ScopeName.XmlComment] = CommentBrush,
            [ScopeName.XmlDocComment] = CommentBrush,
            [ScopeName.XmlDocTag] = CommentBrush,
            [ScopeName.HtmlComment] = CommentBrush,
            // number
            [ScopeName.Number] = NumberBrush,
            [ScopeName.JsonNumber] = NumberBrush,
            // function / type / identifier-ish
            [ScopeName.ClassName] = FunctionBrush,
            [ScopeName.Type] = FunctionBrush,
            [ScopeName.TypeVariable] = FunctionBrush,
            [ScopeName.Constructor] = FunctionBrush,
            [ScopeName.Predefined] = FunctionBrush,
            [ScopeName.Intrinsic] = FunctionBrush,
            [ScopeName.BuiltinFunction] = FunctionBrush,
            [ScopeName.HtmlElementName] = FunctionBrush,
            [ScopeName.XmlAttribute] = FunctionBrush,
            [ScopeName.XmlAttributeQuotes] = FunctionBrush,
        };

    private readonly string _monospaceFontFamily;
    private List<Run> _runs = new();
    private string _source = string.Empty;

    public FlowDocumentCodeColorizer(string monospaceFontFamily)
        : base(Styles: null, languageParser: null)
    {
        _monospaceFontFamily = monospaceFontFamily;
    }

    /// <summary>
    /// Tokenizes <paramref name="code"/> against <paramref name="language"/> and returns the flat
    /// list of colored monospace runs. Concatenating the runs' <c>Text</c> reproduces
    /// <paramref name="code"/> verbatim (incl. newlines). Never throws on valid arguments.
    /// </summary>
    public IReadOnlyList<Run> GetRuns(string code, ILanguage language)
    {
        _runs = new List<Run>();
        _source = code ?? string.Empty;

        // The base parser invokes our Write override via this callback (the canonical drive pattern).
        languageParser.Parse(_source, language, (parsedSourceCode, scopes) => Write(parsedSourceCode, scopes));

        // If the parser produced no callback / no runs (e.g. empty source), still preserve the text.
        if (_runs.Count == 0 && _source.Length > 0)
        {
            _runs.Add(MakeRun(_source, DefaultBrush));
        }

        return _runs;
    }

    /// <summary>
    /// Walks the scope tree and emits one mono <see cref="Run"/> per maximal run of characters that
    /// share an innermost scope color. Gap text outside any scope is emitted as default-foreground.
    /// Fully defensive: out-of-range scope offsets are clamped, so it never throws.
    /// </summary>
    protected override void Write(string parsedSourceCode, IList<Scope> scopes)
    {
        string text = parsedSourceCode ?? string.Empty;
        int length = text.Length;
        if (length == 0)
        {
            return;
        }

        // Per-character color: the brush of the INNERMOST scope covering that character (children
        // applied after parents, so a child scope overrides its parent). Characters in no scope keep
        // the default brush. This flattens ColorCode's nested-scope tree into non-overlapping runs.
        var colors = new SolidColorBrush[length];
        for (int i = 0; i < length; i++)
        {
            colors[i] = DefaultBrush;
        }

        if (scopes is not null)
        {
            foreach (Scope scope in scopes)
            {
                ApplyScope(scope, colors, length);
            }
        }

        // Coalesce consecutive same-brush characters into runs (verbatim text preserved).
        int start = 0;
        while (start < length)
        {
            SolidColorBrush brush = colors[start];
            int end = start + 1;
            while (end < length && ReferenceEquals(colors[end], brush))
            {
                end++;
            }

            _runs.Add(MakeRun(text.Substring(start, end - start), brush));
            start = end;
        }
    }

    private static void ApplyScope(Scope scope, SolidColorBrush[] colors, int length)
    {
        if (scope is null)
        {
            return;
        }

        // Clamp the scope range into [0, length] — never index out of range (AC5 totality).
        int begin = scope.Index;
        if (begin < 0)
        {
            begin = 0;
        }

        long endExclusive = (long)scope.Index + scope.Length;
        int stop = endExclusive > length ? length : (int)endExclusive;

        SolidColorBrush brush = BrushForScope(scope.Name);
        for (int i = begin; i < stop; i++)
        {
            colors[i] = brush;
        }

        // Apply children AFTER the parent so nested (more specific) scopes win.
        IList<Scope> children = scope.Children;
        if (children is not null)
        {
            foreach (Scope child in children)
            {
                ApplyScope(child, colors, length);
            }
        }
    }

    private static SolidColorBrush BrushForScope(string? scopeName)
    {
        if (scopeName is not null && ScopeBrushes.TryGetValue(scopeName, out SolidColorBrush? brush))
        {
            return brush;
        }

        return DefaultBrush;
    }

    private Run MakeRun(string text, SolidColorBrush brush) => new(text)
    {
        FontFamily = new FontFamily(_monospaceFontFamily),
        Foreground = brush,
    };

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
        brush.Freeze();
        return brush;
    }
}
