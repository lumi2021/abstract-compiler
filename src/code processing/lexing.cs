using System.Collections.Generic;

namespace Compiler.CodeProcessing.Lexing;

public static class Lexer
{

    private static readonly Dictionary<string, TokenType[]> lineJunctions = new()
    {
        { "justLeft",  [
            TokenType.LeftBracketChar,
            TokenType.RightParenthesisChar
        ] },
        { "justRight", [
            TokenType.LeftPerenthesisChar
        ] },
        { "bothSides", [
            TokenType.CrossChar, TokenType.MinusChar,
            TokenType.StarChar, TokenType.StarChar, TokenType.PercentChar,
            TokenType.EqualsChar
        ] },
    };

    private static Dictionary<string, TokenType> _keyword2TokenMap = new()
    {
        { "namespace", TokenType.NamespaceKeyword },
        { "using", TokenType.UsingKeyword },

        { "let", TokenType.LetKeyword },
        { "const", TokenType.ConstKeyword },
        { "func", TokenType.FuncKeyword },

        { "return", TokenType.ReturnKeyword },
        { "asm", TokenType.AsmKeyword },

        // values
        { "null", TokenType.NullKeyword },
        { "true", TokenType.TrueKeyword },
        { "false", TokenType.FalseKeyword },

        // types
        { "void", TokenType.TypeKeyword },

                                            { "byte", TokenType.TypeKeyword },
        { "i8", TokenType.TypeKeyword },    { "ui8", TokenType.TypeKeyword },
        { "i16", TokenType.TypeKeyword },   { "ui16", TokenType.TypeKeyword },
        { "i32", TokenType.TypeKeyword },   { "ui32", TokenType.TypeKeyword },
        { "i64", TokenType.TypeKeyword },   { "ui64", TokenType.TypeKeyword },
        // { "i128", TokenType.TypeKeyword },  { "ui128", TokenType.TypeKeyword },

        { "f32", TokenType.TypeKeyword },   { "float", TokenType.TypeKeyword },
        { "f64", TokenType.TypeKeyword },   { "double", TokenType.TypeKeyword },

        { "bool", TokenType.TypeKeyword },
        { "char", TokenType.TypeKeyword },
        { "string", TokenType.TypeKeyword },

    };

    private static Token Tokenize(string value, TokenType type, int start, int end)
        => new() { type = type, value = value, start = start, end = end > 0 ? end : start+1 };

    private static Token Tokenize(char value, TokenType type, int start)
        => Tokenize("" + value, type, start, -1);

    public static Token[] Parse(string sourceCode)
    {
        List<Token> tokens = [];

        for (var i = 0; i < sourceCode.Length; i++)
        {
            char c = sourceCode[i];

            // Check if it's skipable
            if (c == ' ' | c == '\r' | c == '\t') { continue; }

            else if (c == '\n')
                tokens.Add(Tokenize("\\n", TokenType.LineFeed, i, -1));
            else if (c == '(')
                tokens.Add(Tokenize(c, TokenType.LeftPerenthesisChar, i));
            else if (c == ')')
                tokens.Add(Tokenize(c, TokenType.RightParenthesisChar, i));
            else if (c == '{')
                tokens.Add(Tokenize(c, TokenType.LeftBracketChar, i));
            else if (c == '}')
                tokens.Add(Tokenize(c, TokenType.RighBracketChar, i));

            else if (c == '+')
                tokens.Add(Tokenize(c, TokenType.CrossChar, i));
            else if (c == '-')
                tokens.Add(Tokenize(c, TokenType.MinusChar, i));
            else if (c == '*')
                tokens.Add(Tokenize(c, TokenType.StarChar, i));
            else if (c == '/')
                tokens.Add(Tokenize(c, TokenType.SlashChar, i));
            else if (c == '%')
                tokens.Add(Tokenize(c, TokenType.PercentChar, i));
            else if (c == '=')

                tokens.Add(Tokenize(c, TokenType.EqualsChar, i));
            else if (c == ',')
                tokens.Add(Tokenize(c, TokenType.CommaChar, i));

            else 
            {

                // Build number token
                if (char.IsDigit(c))
                {

                    string num = "";

                    int j = i;
                    for ( ; sourceCode.Length > j && char.IsDigit(sourceCode[j]); j++)
                        num += sourceCode[j];

                    tokens.Add(Tokenize(num, TokenType.NumberValue, i, j));

                    i = j-1;
                    
                }
                
                // Build identifier token
                else if (char.IsLetter(c))
                {

                    string token = "";

                    int j = i;
                    for ( ; sourceCode.Length > j && char.IsLetterOrDigit(sourceCode[j]); j++)
                        token += sourceCode[j];

                    if (_keyword2TokenMap.TryGetValue(token, out var type))
                        tokens.Add(Tokenize(token, type, i, j));
                    
                    else tokens.Add(Tokenize(token, TokenType.Identifier, i, j));

                    i = j-1;

                }

                // Build string token
                else if (c == '"')
                {
                    string token = "";

                    int j = i+1;
                    for ( ; sourceCode.Length > j && sourceCode[j] != '"'; j++)
                        token += sourceCode[j];
                    
                    tokens.Add(Tokenize(token, TokenType.StringLiteralValue, i, j));

                    i = j+1;
                }

                // Igonore comments
                else if (c == '#')
                {
                    if (sourceCode.Length > i+3 && sourceCode[i .. (i+3)] == "###")
                    {
                        i+=3;
                        while(sourceCode.Length > i+3 && sourceCode[i .. (i+3)] != "###") i++;
                        i+=3;
                    }
                    else
                    {
                        i++;
                        while(sourceCode.Length > i+1 && (sourceCode[i] != '#' && sourceCode[i] != '\n')) i++;
                        if (sourceCode[i] == '#') i++;
                    }
                }

                // unrecognized character
                else
                {
                    Console.WriteLine($"Error! unrecognized chracter: c");
                    continue;
                }
            
            }
        
        }

        tokens.Add(Tokenize("\\EOF", TokenType.EOFChar, sourceCode.Length, -1));

        VerifyEndOfStatements(tokens);

        return [.. tokens];
    }

    public static void VerifyEndOfStatements(List<Token> tokensList)
    {
        
        List<Token> tokensToEvaluate = new(tokensList);
        int index = 0;

        List<int> indexesToRemove = [];

        while (tokensToEvaluate.Count > 0)
        {
            var currToken = tokensToEvaluate[0];

            if ((lineJunctions["justLeft"].Contains(currToken.type)
            || lineJunctions["bothSides"].Contains(currToken.type)) && index > 0)
            {
                for (int i = index-1; i >= 0; i--)
                {
                    if (tokensList[i].type == TokenType.LineFeed)
                    {
                        indexesToRemove.Add(i);
                    }
                    else break;
                }
            }
            if (lineJunctions["justRight"].Contains(currToken.type)
            || lineJunctions["bothSides"].Contains(currToken.type))
            {
                for (int i = index+1; i <= tokensList.Count; i++)
                {
                    if (tokensList[i].type == TokenType.LineFeed)
                    {
                        indexesToRemove.Add(i);
                        tokensToEvaluate.RemoveAt(1);
                        index++;
                    }
                    else break;
                }
            }

            tokensToEvaluate.Shift();
            index++;

        }

        indexesToRemove.Sort();
        indexesToRemove.Reverse();

        foreach (var i in indexesToRemove.Distinct())
            tokensList.RemoveAt(i);

    }

}

public struct Token {
    public string value;
    public TokenType type;

    public int start;
    public int end;

    public override readonly string ToString() => $"{value} ({type});";
    public readonly string ValueString()
        => type switch
        {
            TokenType.LineFeed or
            TokenType.EOFChar => "\n",
            _ => value,
        };
}

public enum TokenType {
    NumberValue,
    StringLiteralValue,
    Identifier,

    NamespaceKeyword,       // namespace
    UsingKeyword,           // using
    TypeKeyword,
    LetKeyword,             // let
    ConstKeyword,           // const
    FuncKeyword,            // func

    ReturnKeyword,          // return
    AsmKeyword,             // asm

    NullKeyword,            // null
    TrueKeyword,            // true
    FalseKeyword,           // false

    LeftPerenthesisChar,    // (
    RightParenthesisChar,   // )

    LeftBracketChar,        // {
    RighBracketChar,        // }

    CrossChar,              // +
    MinusChar,              // -
    StarChar,               // *
    SlashChar,              // /
    PercentChar,            // %
    EqualsChar,             // =

    CommaChar,              // ,
    EOFChar,                // \EOF

    LineFeed,               // \n
}
