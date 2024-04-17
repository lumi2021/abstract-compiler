using System.Globalization;

namespace Compiler.CodeProcessing;

public static class CodeProcess
{

    public static List<SyntaxError> errors = [];

    public static void Build(string[] toCompile, string outputDir, string outputFile)
    {
        
        Console.WriteLine("Starting build...");

        List<SyntaxTree> syntaxTrees = [];

        int count = 0;
        foreach (string file in toCompile)
        {
            
            var code = File.ReadAllText(file);

            syntaxTrees.Add(ParseSyntaxTree(code, file));

            count++;

        }
    
        Console.WriteLine($"{count} files parsed");
        Console.WriteLine("Compiling...");

        Compilation.Compile([.. syntaxTrees], outputDir, outputFile);

    }

    private static SyntaxTree ParseSyntaxTree(string src, string sourcePath = "")
    {
    // LEXING:
        // GET EXTERNAL DATA
        string[] NasmOps = File.ReadAllLines("src/data/aNasmOps.txt");

        // REMOVE UNECESSARY CHARACTERS
        src = src.Replace("\r", "");

        List<Token> tokens = [];

        // TOKENIZE
        int line = 1;
        for (var i = 0; i < src.Length; i++)
        {

            var c = src[i];

            if (c == '"')
            {
                string token = "";
                for (int j = i+1; j < src.Length; j++)
                {
                    if (src[j] == '"')
                    {
                        i = j;
                        break;
                    }
                    token += src[j];
                }
                tokens.Add(new(TokenKind.StringLiteral, token, i-token.Length,i,line,0));
                continue;
            }

            else if (char.IsLetterOrDigit(c))
            {

                if (char.IsLetter(c))
                {

                    string token = "";

                    for (var j = i; j < src.Length; j++)
                    {
                        var c2 = src[j];

                        if (!char.IsLetterOrDigit(c2)) break;

                        token += c2;
                        i = j;
                    }

                    TokenKind kind = token switch
                    {
                        "using" => TokenKind.UsingKeyword,
                        "namespace" => TokenKind.NamespaceKeyword,
                        "import" => TokenKind.ImportKeyword,

                        "func" => TokenKind.FuncKeyword,
                        "var" => TokenKind.VarKeyword,
                        "return" => TokenKind.ReturnKeyword,

                        "static" => TokenKind.StaticKeyword,
                        "public" => TokenKind.PublicKeyword,
                        "private" => TokenKind.PrivateKeyword,

                        "void" => TokenKind.VoidType,
                        "string" => TokenKind.StringType,
                        "byte" =>  TokenKind.UnsignedByteType,
                        "int" => TokenKind.Integer32Type,

                        _ => TokenKind.Identifier,
                    };

                    if (NasmOps.Contains(token)) kind = TokenKind.AsmOperation;

                    tokens.Add(new(kind, token, i-token.Length,i,line,0));

                }
                else if (char.IsDigit(c))
                {

                    string token = "";

                    for (var j = i; j < src.Length; j++)
                    {
                        var c2 = src[j];

                        if (!char.IsLetterOrDigit(c2) && c2 != '_') break;

                        token += c2;
                        i = j;
                    }

                    TokenKind kind = TokenKind.DecimalNumberValue;

                    token = token.Replace("_", "");

                    if (token.StartsWith("0x")) kind = TokenKind.HexadecNumberValue;
                    if (token.StartsWith("0b")) kind = TokenKind.BinaryNumberValue;

                    tokens.Add(new(kind, token, i,i-token.Length,line,0));

                }

            }

            else if (c != ' ')
            {

                TokenKind kind = c switch
                {
                    '.' => TokenKind.PeriodChar,
                    ',' => TokenKind.CommaChar,
                    '"' => TokenKind.DoubleQuoteChar,
                    '\n' => TokenKind.EndOfLineChar,
                    ':' => TokenKind.ColonChar,
                    ' ' => TokenKind.SpaceChar,

                    '=' => TokenKind.EqualsChar,

                    '(' => TokenKind.LeftParenthesisChar,
                    ')' => TokenKind.RightParenthesisChar,
                    '{' => TokenKind.LeftBraketChar,
                    '}' => TokenKind.RightBraketChar,
                    '[' => TokenKind.LeftSquareBraketChar,
                    ']' => TokenKind.RightSquareBraketChar,

                    _ => TokenKind.UndefinedChar,
                };

                tokens.Add(new(kind, "" + c, i,i+1,line,0));

            }

            if (c == '\n') line++;

        }

        var root = GenerateCompilationNode([.. tokens], sourcePath);

        return SyntaxTree.CreateWithRoot(root);
    }

    private static CompilationNodeSyntax GenerateCompilationNode(Token[] tokens, string sourcePath)
    {

        TokenKind[] validTypes = [
            TokenKind.StringType,
            TokenKind.UnsignedByteType,
            TokenKind.Integer32Type,
            TokenKind.VoidType
        ];

        var root = new CompilationNodeSyntax(sourcePath);
        ScopeNodeSyntax? lastDeclaredScope = null;
        List<ScopeNodeSyntax> scope = [root];


        for (int i = 0; i < tokens.Length; i++)
        {
            var currToken = tokens[i];

            if (lastDeclaredScope != null && currToken.Is(TokenKind.LeftBraketChar))
            {
                scope.Push(lastDeclaredScope);
            }
            else if (lastDeclaredScope != null && !currToken.Is(TokenKind.EndOfLineChar))
            {
                lastDeclaredScope = null;
            }

            if (currToken.Is(TokenKind.RightBraketChar))
            {
                scope.Pop();
            }


            if (currToken.Is(TokenKind.UsingKeyword))
            {
                if (tokens[i+1].Is(TokenKind.NamespaceKeyword))
                {
                    if (scope[0] is CompilationNodeSyntax)
                    {
                        
                        var node = new UsingNamespaceNodeSyntax()
                        .WithIdentifier(new(tokens[i+2].Value));
                        scope[0].AddChildren(node);

                    }
                    else throw new Exception("Error! Namespace declaration is invalid inside an scope!");

                    i+=3;
                }
                else throw new Exception("Error! Invalid using directive! using should be followed by namespace!");
            }

            else if (currToken.Is(TokenKind.FuncKeyword))
            {

                TypeNodeSyntax returnType = null!;
                IdentifierNodeSyntax name = null!;

                if (validTypes.Contains(tokens[i+1].Kind) && tokens[i+2].Is(TokenKind.Identifier))
                {
                    returnType = new TypeNodeSyntax(tokens[i+1].Value);
                    name = new IdentifierNodeSyntax(tokens[i+2].Value);   
                    i += 3;
                }
                else throw new Exception("Error! Invalid function declaration!");
                
                if (tokens[i].Is(TokenKind.LeftParenthesisChar))
                {

                    List<Token> parametersTokens = [];

                    for (var j = i+1; j < tokens.Length; j++)
                    {
                        if (tokens[j].Is(TokenKind.RightParenthesisChar)) break;
                        if (tokens[j].IsEOL()) throw new Exception("Error! No closing parenthesis!");

                        parametersTokens.Add(tokens[j]);

                        i=j;
                    }
                    i++;

                }
                else throw new Exception("Error! A function declaration must declare an argument section!");

                var node = new MethodNodeSyntax()
                .WithIdentifier(name)
                .WithReturnType(returnType);

                lastDeclaredScope = node;

                scope[0].AddChildren(node);

            }

            else if (currToken.Is(TokenKind.VarKeyword))
            {
                TypeNodeSyntax type = null!;
                IdentifierNodeSyntax name = null!;

                if (!validTypes.Contains(tokens[i+1].Kind))
                throw new Exception("Error! Variable type expected!");

                type = new(tokens[i+1].Value);

                if(tokens[i+2].Is(TokenKind.LeftSquareBraketChar)
                && tokens[i+3].Is(TokenKind.RightSquareBraketChar))
                {
                    type.AsArray();
                    i+=2;
                }
            
            
                if (!tokens[i+2].Is(TokenKind.Identifier))
                throw new Exception("Error! Variable name expected!");

                name = new(tokens[i+2].Value);

                var node = new VariableDeclarationSyntax()
                .WithType(type).WithName(name);
                i+=3;

                if (tokens[i].Is(TokenKind.EqualsChar))
                {
                    if (TestNumber(tokens[i+1], out var val))
                        node.WithInitialValue(val);
                    
                    else throw new Exception($"Error! Invalid value \"{tokens[i+1]}\".");
                    
                    i++;
                }

                scope[0].AddChildren(node);

            }

            else if (currToken.Is(TokenKind.AsmOperation))
            {

                List<Token> args = [];

                for (int j = i+1; j < tokens.Length; j+=2)
                {
                    if (tokens[j].IsEOL()) break;

                    args.Add(tokens[j]);
                
                    if (!tokens[j+1].Is(TokenKind.CommaChar) && !tokens[j+1].IsEOL())
                        throw new Exception("Error! Invalid token! expected Comma!"
                        + $"(line {tokens[j+1].span.Line})");
                    
                    if (tokens[j+1].IsEOL()) break;
                    
                    i = j;
                }

                List<NodeSyntax> argsAsTokens = [];
                
                foreach (var tkn in args)
                {
                    if (TestNumber(tkn, out var numeric))
                    {
                        argsAsTokens.Add(numeric);
                    }
                    else
                    {
                        argsAsTokens.Add(new DebugNodeSyntax(tkn.Value));
                    }
                }

                var node = new AsmOperationNodeSyntax(currToken.Value)
                .WithArgs([.. argsAsTokens]);

                scope[0].AddChildren(node);

            }

            else if (currToken.Is(TokenKind.Identifier))
            {
                var identifier = new IdentifierNodeSyntax(currToken.Value);

                while (tokens[i+1].Is(TokenKind.PeriodChar))
                {
                    identifier.Append(tokens[i+2].Value);
                    i+=2;
                }

                if (tokens[i+1].Is(TokenKind.LeftParenthesisChar))
                {

                    List<Token> argsTokens = [];
                    for (var j = i+2; j < tokens.Length; j++)
                    {
                        if (tokens[j].Is(TokenKind.RightParenthesisChar))
                        break;

                        argsTokens.Add(tokens[j]);
                    }


                    var node = new CallNodeSyntax()
                    .WithMethod(identifier)
                    .WithArgs(ParseArgs([.. argsTokens]));

                    scope[0].AddChildren(node);

                }
                else if (tokens[i+1].Is(TokenKind.EqualsChar))
                {
                    if (TestValue(tokens[i+2], out var value))
                    {
                        var node = new AssiginNodeSyntax()
                        .WithDestiny(identifier)
                        .WithValue(value);

                        scope[0].AddChildren(node);
                    }
                }
            }

            else if (currToken.Is(TokenKind.ReturnKeyword))
            {
                scope[0].AddChildren(new ReturnNodeSyntax());
                continue;
            }

        }

        return root;

    }

    private static ArgumentsNodeSyntax ParseArgs(Token[] tokens)
    {
        var node = new ArgumentsNodeSyntax();

        for (var i = 0; i < tokens.Length; i++)
        {

            if (tokens[i].Is(TokenKind.StringLiteral))
            {
                var val = new StringLiteralNodeSyntax(tokens[i].Value);
                node.Append(val);
            }
            else if (TestNumber(tokens[i], out var numeric))
            {
                node.Append(numeric);
            }
            else if (tokens[i].Is(TokenKind.Identifier))
            {
                var val = new IdentifierNodeSyntax(tokens[i].Value);
                node.Append(val);
            }

            if (i+1 < tokens.Length && !tokens[i+1].Is(TokenKind.CommaChar))
                throw new Exception("Error! Expected Comma!");
            else i++;

        }


        return node;
    }

    private static bool TestValue(Token token, out ValueNodeSyntax value)
    {
        
        if (TestNumber(token, out var numericVal))
        {
            value = numericVal;
            return true;
        }
        else if (token.Is(TokenKind.StringLiteral))
        {
            value = new StringLiteralNodeSyntax(token.Value);
            return true;
        }


        value = null!;
        return false;
    }
    private static bool TestNumber(Token token, out NumericValueNodeSyntax numericValueNode)
    {

        if (token.Is(TokenKind.DecimalNumberValue))
        {
            string hexVal = long.Parse(token.Value).ToString("x");
            numericValueNode = (new NumericValueNodeSyntax(hexVal));
            return true;
        }
        else if (token.Is(TokenKind.HexadecNumberValue))
        {
            numericValueNode = new NumericValueNodeSyntax(token.Value[2 ..]);
            return true;
        }
        else if (token.Is(TokenKind.BinaryNumberValue))
        {
            string hexVal = Convert.ToInt64(token.Value[2 ..], 2).ToString("x");
            numericValueNode = new NumericValueNodeSyntax(hexVal);
            return true;
        }

        numericValueNode = null!;
        return false;

    }

}

public class SyntaxTree
{

    private CompilationNodeSyntax _root = null!;

    public CompilationNodeSyntax Root => _root;

    public static SyntaxTree CreateWithRoot(CompilationNodeSyntax root)
    {
        var st = new SyntaxTree();
        st._root = root;
        return st;
    }

    public override string ToString() => _root.ToString();

}


public readonly struct Token(TokenKind kind, string value, int start, int end, int line, int col)
{

    public readonly TokenKind Kind = kind;
    public readonly string Value = value;
    public readonly Span span = new(start, end, line, col);

    public bool Is(TokenKind ofKind) => Kind == ofKind;
    public bool IsEOL() => Kind == TokenKind.EndOfLineChar || Kind == TokenKind.EndOfFileChar;
    public override readonly string ToString() => $"{Value.Replace("\n", "\\n")} ({Kind})";

}

public readonly struct Span(int start, int end, int line, int col)
{
    public readonly int Start = start;
    public readonly int End = end;

    public readonly int Line = line;
    public readonly int Col = col;
}

public struct SyntaxError()
{
    public string message = "Undefined error.";
}

public enum TokenKind : byte
{
    UsingKeyword,           // using
    NamespaceKeyword,       // namespace
    ImportKeyword,          // import

    FuncKeyword,            // func
    VarKeyword,             // var

    StaticKeyword,          // static
    PublicKeyword,          // public
    PrivateKeyword,         // private

    ReturnKeyword,          // return

    StringType,             // string
    UnsignedByteType,       // byte
    Integer32Type,          // int
    VoidType,               // void

    DecimalNumberValue,     // ...
    HexadecNumberValue,     // 0x...
    BinaryNumberValue,      // 0b...

    Identifier,
    StringLiteral,

    AsmOperation,

    CommaChar,              // ,
    PeriodChar,             // .
    DoubleQuoteChar,        // "
    EndOfLineChar,          // \n
    EndOfFileChar,          // \EOF
    ColonChar,              // :
    SpaceChar,              //  

    EqualsChar,             // =

    LeftParenthesisChar,    // (
    RightParenthesisChar,   // )
    LeftBraketChar,         // {
    RightBraketChar,        // }
    LeftSquareBraketChar,   // [
    RightSquareBraketChar,  // ]
    UndefinedChar
}
