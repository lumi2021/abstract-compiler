using Compiler.Util.Compilation;

namespace Compiler;

public static class Extensions
{

    public static T Shift<T>(this List<T> list)
    {
        var item = list[0];
        list.RemoveAt(0);
        return item;
    }

    public static string Shift(this List<char> list)
    {
        var item = list[0];
        list.RemoveAt(0);
        return "" + item;
    }


    public static bool IsValidOnIdentifier(this char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }
    public static bool IsValidOnIdentifierStarter(this char c)
    {
        return char.IsLetter(c) || c == '_';
    }

    private static readonly char[] _languageSymbols = [
        '=', '+', '-', '*', '/', '!', '@', '$', '%', '&', '|', ':', ';', '.', '?', '<', '>'
    ];

    public static bool IsLanguageSymbol(this char c)
        => _languageSymbols.Contains(c);

}
