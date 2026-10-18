using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace NorthWind.RazorComponents.Highlighting;

public static class HighlightHelper
{
    private static readonly CompareInfo CompareInfo = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions MatchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static MarkupString Highlight(string? text, string? searchTerm)
    {
        if (string.IsNullOrEmpty(text))
            return default;

        if (string.IsNullOrWhiteSpace(searchTerm))
            return new MarkupString(WebUtility.HtmlEncode(text));

        string term = searchTerm.Trim();
        int termLength = term.Length;
        if (termLength == 0)
            return new MarkupString(WebUtility.HtmlEncode(text));

        int startIndex = 0;
        var sb = new StringBuilder();

        while (startIndex < text.Length)
        {
            int matchIndex = CompareInfo.IndexOf(text, term, startIndex, MatchOptions);
            if (matchIndex < 0)
            {
                sb.Append(WebUtility.HtmlEncode(text[startIndex..]));
                break;
            }

            if (matchIndex > startIndex)
            {
                sb.Append(WebUtility.HtmlEncode(text[startIndex..matchIndex]));
            }

            int matchLength = -1;
            if (matchIndex + termLength <= text.Length &&
                CompareInfo.Compare(text.Substring(matchIndex, termLength), term, MatchOptions) == 0)
            {
                matchLength = termLength;
            }
            else
            {
                for (int len = 1; len <= text.Length - matchIndex; len++)
                {
                    if (CompareInfo.Compare(text.Substring(matchIndex, len), term, MatchOptions) == 0)
                    {
                        matchLength = len;
                        break;
                    }
                }
            }

            if (matchLength > 0)
            {
                string matchedPart = text.Substring(matchIndex, matchLength);
                sb.Append("<mark class=\"bg-warning text-dark p-0 fw-semibold\">");
                sb.Append(WebUtility.HtmlEncode(matchedPart));
                sb.Append("</mark>");
                startIndex = matchIndex + matchLength;
            }
            else
            {
                sb.Append(WebUtility.HtmlEncode(text.Substring(matchIndex, 1)));
                startIndex = matchIndex + 1;
            }
        }

        return new MarkupString(sb.ToString());
    }
}
