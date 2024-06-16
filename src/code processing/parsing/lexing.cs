using Compiler.CodeProcessing.Scripts;

namespace Compiler.CodeProcessing.Lexing;

public static class Lexer
{
    private static Script currentSrc = null!;


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
            TokenType.CrossChar, TokenType.MinusChar, TokenType.PowerOperator,
            TokenType.StarChar, TokenType.StarChar, TokenType.PercentChar,
            TokenType.EqualsChar,

            TokenType.RightArrowOperator, TokenType.LessEqualsOperator,
            TokenType.EqualOperator, TokenType.GreatEqualsOperator,
            TokenType.UnEqualOperator,

            TokenType.AddAssigin, TokenType.MulAssigin,
            TokenType.SubAssigin, TokenType.DivAssigin
        ] },
    };

    private static Dictionary<string, TokenType> _keyword2TokenMap = new()
    {
        // keywords
        { "namespace", TokenType.NamespaceKeyword },
        { "using", TokenType.UsingKeyword },

        { "let", TokenType.LetKeyword },
        { "const", TokenType.ConstKeyword },
        { "func", TokenType.FuncKeyword },

        { "if", TokenType.IfKeyword },
        { "elif", TokenType.ElifKeyword },
        { "else", TokenType.ElseKeyword },

        { "while", TokenType.WhileKeyword },
        { "for", TokenType.ForKeyword },
        { "do", TokenType.DoKeyword },

        { "return", TokenType.ReturnKeyword },
        { "asm", TokenType.AsmKeyword },

        // values
        { "null", TokenType.NullKeyword },
        { "true", TokenType.TrueKeyword },
        { "false", TokenType.FalseKeyword },

        // types
        { "void", TokenType.TypeKeyword },

                                            { "byte", TokenType.TypeKeyword },
        { "i8", TokenType.TypeKeyword },    { "u8", TokenType.TypeKeyword },
        { "i16", TokenType.TypeKeyword },   { "u16", TokenType.TypeKeyword },
        { "i32", TokenType.TypeKeyword },   { "u32", TokenType.TypeKeyword },
        { "i64", TokenType.TypeKeyword },   { "u64", TokenType.TypeKeyword },
        // { "i128", TokenType.TypeKeyword },  { "ui128", TokenType.TypeKeyword },

        { "f32", TokenType.TypeKeyword },   { "float", TokenType.TypeKeyword },
        { "f64", TokenType.TypeKeyword },   { "double", TokenType.TypeKeyword },

        { "bool", TokenType.TypeKeyword },
        { "char", TokenType.TypeKeyword },
        { "string", TokenType.TypeKeyword },

    };

    private static Token Tokenize(string value, TokenType type, int start, int end)
        => new() { type = type, value = value, start = start, end = end > 0 ? end : start+1, scriptRef = currentSrc};

    private static Token Tokenize(char value, TokenType type, int start)
        => Tokenize("" + value, type, start, -1);

    public static Token[] Parse(Script source)
    {
        currentSrc = source;

        var sourceCode = source.Read();
        List<Token> tokens = [];

        for (var i = 0; i < sourceCode.Length; i++)
        {
            char c = sourceCode[i];
            char c2 = sourceCode.Length > i + 1 ? sourceCode[i+1] : '\0';

            // Check if it's skipable
            if (c == ' ' | c == '\r' | c == '\t') { continue; }

            // Check if it's a multiline character
            if (c.IsLanguageSymbol() && c2.IsLanguageSymbol())
            {
                string cc = $"{c}{c2}";

                TokenType type = cc switch {

                    "=>" => TokenType.RightArrowOperator,
                    "==" => TokenType.EqualOperator,
                    "!=" => TokenType.UnEqualOperator,

                    "<=" => TokenType.LessEqualsOperator,
                    ">=" => TokenType.GreatEqualsOperator,

                    "**" => TokenType.PowerOperator,

                    "+=" => TokenType.AddAssigin,
                    "-=" => TokenType.SubAssigin,
                    "*=" => TokenType.MulAssigin,
                    "/=" => TokenType.DivAssigin,
                    "%=" => TokenType.RestAssigin,

                    _ => TokenType.Undefined
                };

                if (type != TokenType.Undefined)
                {
                    tokens.Add(Tokenize(cc, type, i, i+1));

                    i++;
                    continue;
                }
            }

            // Check single characters
            if (c == '\n')
                tokens.Add(Tokenize("\\n", TokenType.LineFeed, i, -1));
            else if (c == '(')
                tokens.Add(Tokenize(c, TokenType.LeftPerenthesisChar, i));
            else if (c == ')')
                tokens.Add(Tokenize(c, TokenType.RightParenthesisChar, i));
            else if (c == '{')
                tokens.Add(Tokenize(c, TokenType.LeftBracketChar, i));
            else if (c == '}')
                tokens.Add(Tokenize(c, TokenType.RightBracketChar, i));

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

            else if (c == '&')
                tokens.Add(Tokenize(c, TokenType.ComercialEChar, i));

            else if (c == ',')
                tokens.Add(Tokenize(c, TokenType.CommaChar, i));
            else if (c == '.')
                tokens.Add(Tokenize(c, TokenType.DotChar, i));

            else 
            {

                // Build number token
                if (char.IsDigit(c))
                {

                    string num = "";
                    byte numBase = 10;

                    if (c == '0') // verify different bases
                    {
                        if (char.ToLower(sourceCode[i+1]) == 'x')
                        {
                            numBase = 16;
                            i+=2;
                        }
                        if (char.ToLower(sourceCode[i+1]) == 'b')
                        {
                            numBase = 2;
                            i+=2;
                        }
                    }

                    int j = i;
                    for ( ; sourceCode.Length > j; j++)
                    {
                        char cc = sourceCode[j];

                        if (numBase == 10 && !char.IsDigit(cc)) break;
                        else if (numBase == 16 && !char.IsAsciiHexDigit(cc)) break;
                        else if (numBase == 2 && (cc != '0' || cc != '1')) break;

                        num += cc;
                    }

                    tokens.Add(Tokenize(num, TokenType.NumberValue, i, j));

                    i = j-1;
                    
                }
                
                // Build identifier token
                else if (c.IsValidOnIdentifierStarter())
                {

                    string token = "";

                    int j = i;
                    for ( ; sourceCode.Length > j && sourceCode[j].IsValidOnIdentifier(); j++)
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

                    i = j;
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
                    Console.WriteLine($"Error! unrecognized chracter: {c}");
                    continue;
                }
            
            }
        
        }
        sourceCode = "";

        tokens.Add(Tokenize("\\EOF", TokenType.EOFChar, sourceCode.Length, -1));

        VerifyEndOfStatements(tokens);
        
        currentSrc = null!;

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

    public Script scriptRef;

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
    Undefined,

    NumberValue,
    StringLiteralValue,
    Identifier,

    NamespaceKeyword,       // namespace
    UsingKeyword,           // using
    TypeKeyword,
    LetKeyword,             // let
    ConstKeyword,           // const
    FuncKeyword,            // func

    IfKeyword,              // if
    ElifKeyword,            // elif
    ElseKeyword,            // else
    WhileKeyword,           // while
    ForKeyword,             // for
    DoKeyword,              // do
    BreakKeyword,           // break

    ReturnKeyword,          // return
    AsmKeyword,             // asm

    NullKeyword,            // null
    TrueKeyword,            // true
    FalseKeyword,           // false

    LeftPerenthesisChar,    // (
    RightParenthesisChar,   // )

    LeftBracketChar,        // {
    RightBracketChar,       // }

    LeftAngleChar,          // <
    RightAngleChar,         // >

    CrossChar,              // +
    MinusChar,              // -
    StarChar,               // *
    SlashChar,              // /
    PercentChar,            // %
    EqualsChar,             // =
    ComercialEChar,         // &

    RightArrowOperator,     // =>

    EqualOperator,          // ==
    UnEqualOperator,        // !=
    LessEqualsOperator,     // <=
    GreatEqualsOperator,    // >=

    PowerOperator,          // **

    AddAssigin,             // +=
    SubAssigin,             // -=
    MulAssigin,             // *=
    DivAssigin,             // /=
    RestAssigin,            // %=

    CommaChar,              // ,
    DotChar,                // .
    EOFChar,                // \EOF

    LineFeed,               // \n
}

