using System.Text;

namespace Architecture.Tests;

internal static class CSharpLexicalText
{
    public static string RemoveCommentsAndStrings(string text)
    {
        StringBuilder builder = new(text.Length);

        for (int index = 0; index < text.Length;)
        {
            if (StartsWith(text, index, "//"))
            {
                index = SkipSingleLineComment(text, index + 2);
                builder.AppendLine();
                continue;
            }

            if (StartsWith(text, index, "/*"))
            {
                index = SkipMultiLineComment(text, index + 2);
                builder.Append(' ');
                continue;
            }

            if (text[index] == '@' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index = SkipVerbatimString(text, index + 2);
                builder.Append(' ');
                continue;
            }

            if (text[index] == '$' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index = SkipRegularString(text, index + 1);
                builder.Append(' ');
                continue;
            }

            if (text[index] == '$' && index + 2 < text.Length && text[index + 1] == '@' && text[index + 2] == '"')
            {
                index = SkipVerbatimString(text, index + 3);
                builder.Append(' ');
                continue;
            }

            if (text[index] == '@' && index + 2 < text.Length && text[index + 1] == '$' && text[index + 2] == '"')
            {
                index = SkipVerbatimString(text, index + 3);
                builder.Append(' ');
                continue;
            }

            if (text[index] is '"' or '\'')
            {
                index = SkipRegularString(text, index);
                builder.Append(' ');
                continue;
            }

            builder.Append(text[index]);
            index++;
        }

        return builder.ToString();
    }

    private static bool StartsWith(string text, int index, string value)
        => index + value.Length <= text.Length
            && string.Compare(text, index, value, 0, value.Length, StringComparison.Ordinal) == 0;

    private static int SkipSingleLineComment(string text, int index)
    {
        while (index < text.Length && text[index] is not '\r' and not '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipMultiLineComment(string text, int index)
    {
        while (index + 1 < text.Length && !StartsWith(text, index, "*/"))
        {
            index++;
        }

        return Math.Min(index + 2, text.Length);
    }

    private static int SkipRegularString(string text, int index)
    {
        char quote = text[index];
        index++;

        while (index < text.Length)
        {
            if (text[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (text[index] == quote)
            {
                return index + 1;
            }

            index++;
        }

        return text.Length;
    }

    private static int SkipVerbatimString(string text, int index)
    {
        while (index < text.Length)
        {
            if (text[index] == '"' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index += 2;
                continue;
            }

            if (text[index] == '"')
            {
                return index + 1;
            }

            index++;
        }

        return text.Length;
    }
}
