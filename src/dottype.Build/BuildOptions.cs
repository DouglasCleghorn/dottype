using System.Text;
using dottype;

namespace dottype.Build;

internal static class BuildOptions
{
    public static CliOptions Parse(string settings, string projectDirectory, bool requireInputExists = true)
        => CliOptions.Parse(Tokenize(settings), requireInputExists,
            buildIntegration: true, baseDirectory: projectDirectory);

    // Quotes group spaces; backslashes are literal so Windows paths survive.
    // This is an argument parser, never a shell command.
    internal static List<string> Tokenize(string settings)
    {
        var result = new List<string>();
        var token = new StringBuilder();
        char quote = '\0';
        bool started = false;
        foreach (char character in settings)
        {
            if (quote != '\0')
            {
                if (character == quote) quote = '\0';
                else token.Append(character);
            }
            else if (character is '\'' or '"')
            {
                quote = character;
                started = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (started) result.Add(token.ToString());
                token.Clear();
                started = false;
            }
            else
            {
                token.Append(character);
                started = true;
            }
        }
        if (quote != '\0') throw new CliException("Unterminated quote in the dottype property.");
        if (started) result.Add(token.ToString());
        return result;
    }
}
