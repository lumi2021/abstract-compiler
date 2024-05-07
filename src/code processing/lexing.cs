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
        { "let", TokenType.LetKeyword },
        { "const", TokenType.ConstKeyword },
        { "func", TokenType.FuncKeyword },
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
        { "i128", TokenType.TypeKeyword },  { "ui128", TokenType.TypeKeyword },

        { "f32", TokenType.TypeKeyword },   { "float", TokenType.TypeKeyword },
        { "f64", TokenType.TypeKeyword },   { "double", TokenType.TypeKeyword },

        { "bool", TokenType.TypeKeyword },
        { "char", TokenType.TypeKeyword },
        { "string", TokenType.TypeKeyword },

    };

    private static Token Tokenize(string value, TokenType type)
        => new() { type = type, value = value };

    public static Token[] Parse(string sourceCode)
    {
        List<Token> tokens = [];
        List<char> src = [.. sourceCode.ToCharArray()];

        while (src.Count > 0)
        {
            char c = src[0];

            // Check if it's skipable
            if (c == ' ' | c == '\r' | c == '\t') { src.Shift(); continue; }

            else if (c == '\n')
            {
                src.Shift();
                tokens.Add(Tokenize("\\n", TokenType.LineFeed));
            }
            else if (c == '(')
                tokens.Add(Tokenize(src.Shift(), TokenType.LeftPerenthesisChar));
            else if (c == ')')
                tokens.Add(Tokenize(src.Shift(), TokenType.RightParenthesisChar));
            else if (c == '{')
                tokens.Add(Tokenize(src.Shift(), TokenType.LeftBracketChar));
            else if (c == '}')
                tokens.Add(Tokenize(src.Shift(), TokenType.RighBracketChar));

            else if (c == '+')
                tokens.Add(Tokenize(src.Shift(), TokenType.CrossChar));
            else if (c == '-')
                tokens.Add(Tokenize(src.Shift(), TokenType.MinusChar));
            else if (c == '*')
                tokens.Add(Tokenize(src.Shift(), TokenType.StarChar));
            else if (c == '/')
                tokens.Add(Tokenize(src.Shift(), TokenType.SlashChar));
            else if (c == '%')
                tokens.Add(Tokenize(src.Shift(), TokenType.PercentChar));
            else if (c == '=')

                tokens.Add(Tokenize(src.Shift(), TokenType.EqualsChar));
            else if (c == ',')
                tokens.Add(Tokenize(src.Shift(), TokenType.CommaChar));


            else 
            {

                // Build number token
                if (char.IsDigit(c))
                {

                    string num = "";

                    while (src.Count > 0 && char.IsDigit(src[0]))
                        num += src.Shift();

                    tokens.Add(Tokenize(num, TokenType.NumberValue));

                }
                
                // Build identifier token
                else if (char.IsLetter(c))
                {

                    string token = "";

                    while (src.Count > 0 && char.IsLetterOrDigit(src[0]))
                    {
                        token += src.Shift();
                    }

                    if (_keyword2TokenMap.TryGetValue(token, out var type))
                        tokens.Add(Tokenize(token, type));
                    
                    else tokens.Add(Tokenize(token, TokenType.Identifier));

                }


                // unrecognized character
                else
                {
                    Console.WriteLine($"Error! unrecognized chracter: c");
                    src.Shift();
                }
            
            }

        }

        tokens.Add(Tokenize("\\EOF", TokenType.EOFChar));

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
    Identifier,

    TypeKeyword,
    LetKeyword,             // let
    ConstKeyword,           // const
    FuncKeyword,            // func

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
