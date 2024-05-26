using System.Text;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.Lexing;

namespace Compiler.CodeProcessing.Parsing;

public static class Parser
{
    private static List<Token> tokens = [];

    public static ScriptNode ParseTokens(Token[] source)
    {
        tokens = [.. source];

        var scriptNode = new ScriptNode();

        while(!Current().IsEOF())
        {
            if (Current().type == TokenType.LineFeed) {Eat(); continue;}
            scriptNode.body.Add(ParseStatement());
        }

        return scriptNode;
    }

    private static bool IsEOF(this Token token) => token.type == TokenType.EOFChar;
    private static Token Current() => tokens[0];
    private static Token Next(int offset = 1) => tokens[0 + offset];
    private static Token Eat() => tokens.Shift();
    private static Token Expect(TokenType expectType, string errorMsg = "")
    {
        var tkn = Eat();

        if (tkn.type != expectType)
        {
            Console.WriteLine($"{errorMsg} ({tkn}, {tkn.start} - {tkn.end})");
            Environment.Exit(1);
        }
        
        return tkn;
    }

    #region Statements parsing
    private static StatementNode ParseStatement()
    {
        return Current().type switch
        {
            TokenType.LetKeyword or
            TokenType.ConstKeyword => ParseVariableDeclaration(),
            TokenType.FuncKeyword => ParseMethodDeclaration(),

            TokenType.NamespaceKeyword => ParseNamespaceDeclaration(),

            TokenType.ReturnKeyword => ParseReturnStatement(),
            TokenType.AsmKeyword => ParseInlineAsm(),

            _ => ParseExpression(),
        };
    }

    private static NamespaceNode ParseNamespaceDeclaration()
    {
        Eat();

        var name = Expect(TokenType.Identifier, "Identifiex expected when declaring an namespace!").ValueString();

        var scope = ParseScope();

        var namespaceNode = new NamespaceNode()
        {
            name = new(null!, name),
            namespaceScope = scope
        };

        return namespaceNode;
    }

    private static VariableDeclarationNode ParseVariableDeclaration()
    {
        bool isConstant = Eat().type == TokenType.ConstKeyword;
        TypeNode type = ParseType();
        string identifier = Expect(TokenType.Identifier, $"Identifier expected in a variable declaration!").value;
        ExpressionNode? value = null;

        if (Current().type == TokenType.EqualsChar)
        {
            Eat();
            value = ParseExpression();
        }

        Expect(TokenType.LineFeed, "Variable declaration need to be finished to declarate another expression!");

        return new()
        {
            isConstant = isConstant,
            identifier = new(null!, identifier),
            value = value,
            type = type
        };
    }
    
    private static MethodDeclarationNode ParseMethodDeclaration()
    {
        Eat(); // jump func keyword
        TypeNode returnType = ParseType();
        string identifier = Expect(TokenType.Identifier, $"Identifier expected in the method declaratioon!").value;
    
        var parameters = ParseMethodParameters();

        var scope = ParseScope();

        Expect(TokenType.LineFeed, "Statement must be ended after a Method Declaration!");

        return new()
        {
            returnType = returnType,
            name = new(null!, identifier),
            parameters = parameters,
            methodScope = scope
        };
    }
    
    private static ParametersListNode ParseMethodParameters()
    {
        var list = new ParametersListNode();

        Expect(TokenType.LeftPerenthesisChar, "Parenthesis missing at the end of the method declaration!");

        while (true)
        {
            if (Current().type == TokenType.RightParenthesisChar) break;

            TypeNode type = ParseType();
            string identifier = Expect(TokenType.Identifier, $"Identifier expected after type in parameter declaratioon!").value;
            
            list.parameters.Add(new()
            {
                type = type,
                identifier = identifier
            });

            if (Current().type == TokenType.RightParenthesisChar) break;
            else if (Current().type == TokenType.CommaChar) { Eat(); continue; }
            else Expect(TokenType.CommaChar, "Expected Comma!");

        }

        Expect(TokenType.RightParenthesisChar, $"Closing parenthesis missing at the end of the method declaration!");


        return list;
    }
    
    private static ScopeNode ParseScope()
    {
        var scope = new ScopeNode();

        Expect(TokenType.LeftBracketChar, "Left bracket expected to open the scope!");
    
        while(Current().type != TokenType.RighBracketChar)
        {
            if (Current().type == TokenType.LineFeed) {Eat(); continue;}
            if (Current().IsEOF()) Expect(TokenType.RighBracketChar, $"Expected '}}' at the end of the scope!");

            scope.body.Add(ParseStatement());
        }

        Expect(TokenType.RighBracketChar, "Expected '}' at the end of the scope!");

        return scope;
    }
    
    private static ReturnStatementNode ParseReturnStatement()
    {
        Eat();
        ExpressionNode? value = null;
        if (Current().type != TokenType.LineFeed)
            value = ParseExpression();
        
        return new() {
            value = value
        };
    }
    #endregion

    #region Expressions parsing
    private static ExpressionNode ParseExpression()
    {
        return ParseAssiginmentExpression();
    }

    private static ExpressionNode ParseAssiginmentExpression()
    {
        var left = ParseAdditiveExpression();

        while (Current().type == TokenType.EqualsChar)
        {
            Eat();
            var value = ParseExpression();
            left = new AssiginmentExpressionNode()
            {
                assigne = left,
                value = value
            };
        }

        return left;
    }

    private static ExpressionNode ParseAdditiveExpression()
    {
        var left = ParseMultiplicativeExpression();

        while (Current().value == "+" || Current().value == "-")
        {
            var op = Eat().value;
            var right = ParseMultiplicativeExpression();

            left = new BinaryExpressionNode()
            {
                expOperator = op,
                leftStatement = left,
                rightStatement = right
            };
        }

        return left;
    }

    private static ExpressionNode ParseMultiplicativeExpression()
    {
        var left = ParseUnaryExpression();

        while (Current().value == "*" || Current().value == "/" || Current().value == "%")
        {
            var op = Eat().value;
            var right = ParseUnaryExpression();

            left = new BinaryExpressionNode()
            {
                expOperator = op,
                leftStatement = left,
                rightStatement = right
            };
        }

        return left;
    }

    private static ExpressionNode ParseUnaryExpression()
    {
        var v = Current().value;

        if (v == "-" || v == "+")
        {

            return new UnaryExpressionNode()
            {
                expOperator = Eat().value,
                expression = ParsePrimaryExpression()
            };

        }
        else if ("*/%".Contains(v))
            Expect(TokenType.MinusChar, $"Invalid unary operator {v}!");


        return ParsePrimaryExpression();
    }

    private static ExpressionNode ParsePrimaryExpression()
    {
        var tkType = Current().type;

        switch(tkType)
        {
            // Identifiers
            case TokenType.Identifier:

                if (Next().type == TokenType.LeftPerenthesisChar)
                    return ParseMethodCall();
                else return new IdentifierNode() { symbol = new(null!, Eat().value) };

            // Constant/literal numeric values
            case TokenType.NumberValue:
                return new NumericLiteralNode() { value = Double.Parse(Eat().value) };

            // Constant/literal string values
            case TokenType.StringLiteralValue:
                return new StringLiteralNode() { value = Eat().value };

            // Null literal value
            case TokenType.NullKeyword:
                Eat(); // advance
                return new NullLiteralNode();

            // Parenthesis grouped expressions
            case TokenType.LeftPerenthesisChar:
                Eat();
                var val = ParseExpression();
                Expect(TokenType.RightParenthesisChar, $"Unexpected token! Expected Closing parenthesis.");
                return val;

            default: throw new NotImplementedException($"{tkType}");
        }
    }
    
    private static MethodCallNode ParseMethodCall()
    {
        var methodName = Eat();
        Expect(TokenType.LeftPerenthesisChar, "Unexpected token! Expected oppening parenthesis!");
        Expect(TokenType.RightParenthesisChar, "Unexpected token! Expected closing parenthesis!");

        return new MethodCallNode() { target = new Identifier(null!, [methodName.value]) };
    }
    #endregion

    #region Assembly parsing

    private static StatementNode ParseInlineAsm()
    {
        Eat(); // asm token
        if (Current().type == TokenType.LeftBracketChar)
            return ParseAsmScope();
        else
            return ParseAsmExpression();
    }

    private static AssemblyScopeNode ParseAsmScope()
    {
        var scope = new AssemblyScopeNode();

        Expect(TokenType.LeftBracketChar, "Left bracket expected to open the scope!");

        while (Current().type != TokenType.RighBracketChar)
        {
            if (Current().type == TokenType.LineFeed) {Eat(); continue;}
            if (Current().IsEOF()) Expect(TokenType.RighBracketChar, $"Expected '}}' at the end of the scope!");

            scope.body.Add(ParseAsmExpression());
        }

        Expect(TokenType.RighBracketChar, "Expected '}' at the end of the scope!");

        return scope;
    }

    private static AssemblyExpressionNode ParseAsmExpression()
    {
        var esp = new AssemblyExpressionNode();

        var inst = Eat();

        esp.instruction = inst.value.ToLower() switch
        {
            "add" => AssemblyInstruction.Add,
            "div" => AssemblyInstruction.Div,
            "mov" => AssemblyInstruction.Mov,
            "mul" => AssemblyInstruction.Mul,
            "push" => AssemblyInstruction.Push,
            "sub" => AssemblyInstruction.Sub,

            _ => AssemblyInstruction.Undefined
        };
        
        while (true)
        {
            esp.arguments.Add(Eat().value);

            if (Current().type == TokenType.LineFeed) break;
            else Expect(TokenType.CommaChar, "Expected comma!");
        }

        return esp;

    }

    #endregion

    #region Other parsings
    private static TypeNode ParseType()
    {
        var token = Expect(TokenType.TypeKeyword, "Type expected!");

        #region switch
        PrimitiveType typeValue = token.value switch
        {
            "void" => PrimitiveType.Void,
            // integers
            "i8" => PrimitiveType.Integer_8,
            "i16" => PrimitiveType.Integer_16,
            "i32" => PrimitiveType.Integer_32,
            "i64" => PrimitiveType.Integer_64,
            "i128" => PrimitiveType.Integer_128,

            // unsigned integers
            "ui8" or "byte" => PrimitiveType.UnsignedInteger_8,
            "ui16" => PrimitiveType.UnsignedInteger_16,
            "ui32" => PrimitiveType.UnsignedInteger_32,
            "ui64" => PrimitiveType.UnsignedInteger_64,
            "ui128" => PrimitiveType.UnsignedInteger_128,

            // floats
            "f32" or "float" => PrimitiveType.Floating_32,
            "f64" or "double" => PrimitiveType.Floating_64,

            // boolean
            "bool" => PrimitiveType.Boolean,

            // text
            "char" => PrimitiveType.Character,
            "string" => PrimitiveType.String,

            _ => throw new NotImplementedException(),
        };
        #endregion

        // TODO handle arrays

        return new PrimitiveTypeNode()
        {
            value = typeValue,
        };
    }
    #endregion

}

public static class AstWriter
{

    public static void WriteAst(ScriptNode program, string oPath)
    {
        StringBuilder final = new();

        final.AppendLine("### Abstract AST parsing result! ###\n");

        foreach (var i in program.body)
        {
            final.AppendLine(ProcessStatement(i));
        }


        if (!Directory.Exists(oPath))
            Directory.CreateDirectory(oPath);
        
        File.WriteAllText(oPath + "/parsed-AST.txt", final.ToString().Replace("\r\n", "\n"));
    }

    
    private static string ProcessScope(ScopeNode scope)
    {
        StringBuilder scopeStr = new();

        scopeStr.AppendLine("{");
        foreach (var i in scope.body)
        {

            var resStr = ProcessStatement(i);
            var res = resStr.Split('\n');

            foreach (var j in res) scopeStr.AppendLine($"\t{j}");

        }
        scopeStr.Append('}');

        return scopeStr.ToString();
    }

    private static string ProcessAsmScope(AssemblyScopeNode scope)
    {
        StringBuilder scopeStr = new();

        scopeStr.AppendLine("[Assembly x86_64]\n{");
        foreach (var i in scope.body)
        {

            scopeStr.Append($"\t{i.instruction.ToString().ToUpper(),-4}");
            scopeStr.Append($"\t{string.Join(", ", i.arguments)}\n");

        }
        scopeStr.Append('}');

        return scopeStr.ToString();
    }


    private static string ProcessStatement(StatementNode statement)
    {
        if (statement is VariableDeclarationNode @varDec)
        {
            
            string str = "";
            str += @varDec.isConstant ? "const" : "let";
            str += $" {@varDec.type}";
            str += $" {@varDec.identifier}";

            if (@varDec.value != null)
            {
                str += " = ";
                str += ProcessExpression(@varDec.value);
            }

            return str;
        }
        else if (statement is MethodDeclarationNode @metDec)
        {
            var str = "";
            str += $"func {@metDec.returnType} {@metDec.name} ({@metDec.parameters})";
            
            if (@metDec.methodScope.body.Count > 0)
                str += '\n' + ProcessScope(@metDec.methodScope);
            else str += ';';
            
            return str;
        }


        else if (statement is NamespaceNode @nmspaceDec)
        {
            var str = "";
            str += $"namespace {@nmspaceDec.name}";

            str += '\n' + ProcessScope(@nmspaceDec.namespaceScope);

            return str;
        }

        else if (statement is AssemblyScopeNode @asmScp)
        {
            return ProcessAsmScope(@asmScp);
        }
        else if (statement is AssemblyExpressionNode @asmExp)
        {
            return $"[asm x86_64] {@asmExp.instruction.ToString().ToUpper(),-8} {string.Join(", ", @asmExp.arguments)}";
        }

        else if (statement is AssiginmentExpressionNode)
        {
            var sttm = (statement as AssiginmentExpressionNode)!;

            string str = "";

            do
            {
                str += $"{ProcessExpression(sttm.assigne)} = ";

                if (sttm.value is not AssiginmentExpressionNode)
                {
                    str += $"{ProcessExpression(sttm.value)}";
                }

                sttm = sttm.value as AssiginmentExpressionNode;
            }
            while (sttm is not null);

            return str;
        }
        else if (statement is ExpressionNode @exp)
        {
            return ProcessExpression(@exp);
        }

        return $"<# undefined statement {statement.GetType().Name} #>";
    }

    private static string ProcessExpression(ExpressionNode expression)
    {

        if (expression is NumericLiteralNode @numericLiteral)
            return @numericLiteral.value.ToString();
        else if (expression is StringLiteralNode @stringLiteral)
            return $"\"{@stringLiteral.value}\"";
        
        else if (expression is BinaryExpressionNode @binaryExp)
            return ProcessBinaryNode(@binaryExp);
        else if (expression is UnaryExpressionNode @unaryExp)
            return ProcessUnaryNode(@unaryExp);
        
        else if (expression is MethodCallNode @methodCall)
            return @methodCall.ToString();
        
        else if (expression is IdentifierNode @identifier)
            return @identifier.symbol.ToString();

        else throw new NotImplementedException(expression.GetType().Name);
    }

    private static string ProcessBinaryNode(BinaryExpressionNode binaryExp)
    {

        var left = ProcessExpression(binaryExp.leftStatement);
        var right = ProcessExpression(binaryExp.rightStatement);

        if (binaryExp.expOperator == "*"
        || binaryExp.expOperator == "/"
        || binaryExp.expOperator == "%")
        {
            if (binaryExp.leftStatement is BinaryExpressionNode @leftb &&
            (@leftb.expOperator == "+" || @leftb.expOperator == "-"))
                left = $"({left})";
            
            if (binaryExp.rightStatement is BinaryExpressionNode @rightb &&
            (@rightb.expOperator == "+" || @rightb.expOperator == "-"))
                right = $"({right})";
            
        }

        return $"{left} {binaryExp.expOperator} {right}";
    }
    private static string ProcessUnaryNode(UnaryExpressionNode unaryExp)
    {
        return unaryExp.expOperator switch
        {
            "+" => ProcessExpression(unaryExp.expression),
            "-" => "-" + ProcessExpression(unaryExp.expression),

            _   => "NaN"
        };
    }


}
