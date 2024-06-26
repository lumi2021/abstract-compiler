using System.Globalization;
using System.Text;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.Exeptions;
using Compiler.CodeProcessing.Lexing;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;

namespace Compiler.CodeProcessing.Parsing;

public static class Parser
{
    private static Script currentSrc = null!;
    private static List<Token> tokens = [];

    public static ScriptNode ParseTokens(Token[] source, Script srcRef)
    {
        currentSrc = srcRef;
        tokens = [.. source];

        var scriptNode = new ScriptNode(srcRef);

        while(!Current().IsEOF())
        {
            if (Current().type == TokenType.LineFeed) {Eat(); continue;}
            var stat = ParseStatement();
            if (stat != null) scriptNode.body.Add(stat);
        }

        currentSrc = null!;
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
    private static StatementNode? ParseStatement()
    {
        return Current().type switch
        {
            TokenType.LetKeyword or
            TokenType.ConstKeyword => ParseVariableDeclaration(),
            TokenType.FuncKeyword => ParseMethodDeclaration(),

            TokenType.NamespaceKeyword => ParseNamespaceDeclaration(),

            TokenType.ReturnKeyword => ParseReturnStatement(),
            TokenType.AsmKeyword => ParseInlineAsm(),

            TokenType.IfKeyword => ParseIfStatement(),

            _ => ParseExpression(),
        };
    }

    private static NamespaceNode ParseNamespaceDeclaration()
    {
        Eat();

        var name = ParseSymbol();

        var scope = ParseScope();

        var namespaceNode = new NamespaceNode()
        {
            name = name,
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

        ScopeNode scope = null!;

        if (currentSrc is not HeaderScript)
            scope = ParseScope();

        Expect(TokenType.LineFeed, "Statement must be ended after a Method Declaration!");

        return new()
        {
            returnType = returnType,
            name = new(null!, identifier),
            parameters = parameters,
            methodScope = scope ?? new()
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
    
        while(Current().type != TokenType.RightBracketChar)
        {
            if (Current().type == TokenType.LineFeed) {Eat(); continue;}
            if (Current().IsEOF()) Expect(TokenType.RightBracketChar, $"Expected '}}' at the end of the scope!");

            var stat = ParseStatement();
            if (stat != null) scope.body.Add(stat);
        }

        Expect(TokenType.RightBracketChar, "Expected '}' at the end of the scope!");

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
    
    private static IfStatementNode ParseIfStatement()
    {
        var ifStat = new IfStatementNode();

        Eat();

        ifStat.condition = ParseExpression();
        Expect(TokenType.RightArrowOperator, "Expected right arow after conditional expression and before the conditioned statement!");
        ifStat.result = ParseStatement();

        while (Current().type == TokenType.LineFeed) Eat();

        if (Current().type == TokenType.ElifKeyword)
            ifStat.elseStatement = ParseElifStatement();
        
        else if (Current().type == TokenType.ElseKeyword)
            ifStat.elseStatement = ParseElseStatement();

        return ifStat;
    }
    
    private static ElseStatementNode ParseElifStatement()
    {
        var elseStat = new ElseStatementNode();

        Eat();

        elseStat.condition = ParseExpression();
        Expect(TokenType.RightArrowOperator, "Expected right arow after conditional expression and before the conditioned statement!");
        elseStat.result = ParseStatement();

        while (Current().type == TokenType.LineFeed) Eat();

        if (Current().type == TokenType.ElifKeyword)
            elseStat.elseStatement = ParseElifStatement();
        
        else if (Current().type == TokenType.ElseKeyword)
            elseStat.elseStatement = ParseElseStatement();

        return elseStat;
    }

    private static ElseStatementNode ParseElseStatement()
    {
        var elseStat = new ElseStatementNode();

        Eat();

        Expect(TokenType.RightArrowOperator, "Expected right arow after 'else' and before the conditioned statement!");
        elseStat.result = ParseStatement();

        return elseStat;
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
                expOperator = Eat(),
                expression = ParseTypeCasting()
            };

        }
        else if ("*/%".Contains(v))
            Expect(TokenType.MinusChar, $"Invalid unary operator {v}!");

        else if (Current().type == TokenType.ComercialEChar)
        {
            return new ReferenceModifier()
            {
                modifier = Eat(),
                expression = ParseUnaryExpression()
            };
        }

        return ParseTypeCasting();
    }

    private static ExpressionNode ParseTypeCasting()
    {
        var exp = ParsePrimaryExpression();

        if (Current().type == TokenType.AsKeyword)
        {
            Eat();
            return new TypeCastingExpressionNode()
            {
                expression = exp,
                type = ParseType()
            };
        }

        return exp;
    }

    private static ExpressionNode ParsePrimaryExpression()
    {
        var tk = Current();

        switch(tk.type)
        {
            // Identifiers
            case TokenType.Identifier:
                var symbol = ParseSymbol();
                if (Current().type == TokenType.LeftPerenthesisChar)
                    return ParseMethodCall(symbol); 
                else return new IdentifierNode() { symbol = symbol };

            // Constant/literal integer numeric values
            case TokenType.IntegerNumberValue:
                return new NumericLiteralNode() { value = long.Parse(Eat().value) };

            // Constant/literal floating numeric values
            case TokenType.FloatingNumberValue:
                return new FloatingLiteralNode() { value = double.Parse(Eat().value, CultureInfo.InvariantCulture) };

            // Constant/literal string values
            case TokenType.StringLiteralValue:
                return new StringLiteralNode() { value = Eat().value };
            
            // Constant/literal boolean values
            case TokenType.TrueKeyword:
            case TokenType.FalseKeyword:
                return new BooleanLiteralNode() { value = Eat().value == "true" };

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

            default: // unespected expression
                currentSrc.ThrowError(new UnexpectedTokenException(tk));
                Eat();
                return null!;
        }
    }
    
    private static MethodCallNode ParseMethodCall(Identifier symbol)
    {
        Expect(TokenType.LeftPerenthesisChar, "Unexpected token! Expected oppening parenthesis!");

        List<ExpressionNode> args = [];

        if (Current().type != TokenType.RightParenthesisChar) do {
            if (Current().type == TokenType.CommaChar) Eat();
            args.Add(ParseExpression());
        } while (Current().type == TokenType.CommaChar);

        Expect(TokenType.RightParenthesisChar, "Unexpected token! Expected closing parenthesis!");

        return new MethodCallNode() { target = symbol, arguments = args };
    }
    #endregion

    #region Assembly parsing

    private static StatementNode? ParseInlineAsm()
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

        while (Current().type != TokenType.RightBracketChar)
        {
            if (Current().type == TokenType.LineFeed) {Eat(); continue;}
            if (Current().IsEOF()) Expect(TokenType.RightBracketChar, $"Expected '}}' at the end of the scope!");

            var asm = ParseAsmExpression();
            if (asm != null) scope.body.Add(asm);
        }

        Expect(TokenType.RightBracketChar, "Expected '}' at the end of the scope!");

        return scope;
    }

    private static AssemblyExpressionNode? ParseAsmExpression()
    {
        var esp = new AssemblyExpressionNode();

        var inst = Eat();

        esp.instruction = inst.value.ToLower() switch
        {
            "push" => AssemblyInstruction.Push,

            "add" => AssemblyInstruction.Add,
            "div" => AssemblyInstruction.Div,
            "mov" => AssemblyInstruction.Mov,
            "mul" => AssemblyInstruction.Mul,
            "sub" => AssemblyInstruction.Sub,

            "call" => AssemblyInstruction.Call,

            "extern" => AssemblyInstruction.Extern,

            _ => AssemblyInstruction.Undefined
        };
        
        while (true)
        {
            esp.arguments.Add(ParseExpression());

            if (Current().type == TokenType.LineFeed) break;
            else Expect(TokenType.CommaChar, "Expected comma between assembly instruction parameters!");
        }

        return esp;
    }

    #endregion

    #region Other parsings
    private static TypeNode ParseType()
    {
        var token = Expect(TokenType.TypeKeyword, "Type expected!");

        #region switch
        PrimitiveTypeList typeValue = token.value switch
        {
            "void" => PrimitiveTypeList.Void,
            // integers
            "i8" => PrimitiveTypeList.Integer_8,
            "i16" => PrimitiveTypeList.Integer_16,
            "i32" => PrimitiveTypeList.Integer_32,
            "i64" => PrimitiveTypeList.Integer_64,
            "i128" => PrimitiveTypeList.Integer_128,

            // unsigned integers
            "u8" or "byte" => PrimitiveTypeList.UnsignedInteger_8,
            "u16" => PrimitiveTypeList.UnsignedInteger_16,
            "u32" => PrimitiveTypeList.UnsignedInteger_32,
            "u64" => PrimitiveTypeList.UnsignedInteger_64,
            "u128" => PrimitiveTypeList.UnsignedInteger_128,

            // floats
            "f32" or "float" => PrimitiveTypeList.Floating_32,
            "f64" or "double" => PrimitiveTypeList.Floating_64,

            // boolean
            "bool" => PrimitiveTypeList.Boolean,

            // text
            "char" => PrimitiveTypeList.Character,
            "string" => PrimitiveTypeList.String,

            _ => throw new NotImplementedException(),
        };
        #endregion

        // TODO handle arrays

        return new PrimitiveTypeNode()
        {
            value = typeValue,
        };
    }
    
    private static Identifier ParseSymbol(TypeItem reffersTo = null!)
    {
        var symbol = new Identifier(reffersTo, [Eat().value]);

        while (true)
        {
            if (Current().type == TokenType.DotChar)
            {
                Eat();
                symbol.values.Add(Expect(TokenType.Identifier, "Identifier expected!").value);
            }
            else break;
        }

        return symbol;
    }
    #endregion

}

public static class AstWriter
{

    public static void WriteAst(ScriptNode program, string oPath, string sourceName)
    {
        StringBuilder final = new();

        final.AppendLine("### Abstract AST parsing result! ###\n");

        foreach (var i in program.body)
        {
            final.AppendLine(ProcessStatement(i));
        }

        if (!Directory.Exists($"{oPath}/AST/"))
            Directory.CreateDirectory($"{oPath}/AST/");
        
        File.WriteAllText($"{oPath}/AST/{sourceName}-AST.txt", final.ToString().Replace("\r\n", "\n"));
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

        else if (statement is ReturnStatementNode @ret)
        {
            return "return " + (@ret.value != null ? $"{ProcessStatement(@ret.value)}" : "");
        }

        else if (statement is IfStatementNode @ifstat)
        {
            return $"{@ifstat}";
        }

        return $"<# undefined statement {statement.GetType().Name} #>";
    }

    private static string ProcessExpression(ExpressionNode expression)
    {

        if (expression is NumericLiteralNode @numericLiteral)
            return @numericLiteral.ToString();
        else if (expression is FloatingLiteralNode @floatingLiteral)
            return @floatingLiteral.ToString();
        else if (expression is StringLiteralNode @stringLiteral)
            return $"\"{@stringLiteral.value}\"";
        
        else if (expression is BinaryExpressionNode @binaryExp)
            return ProcessBinaryNode(@binaryExp);
        else if (expression is UnaryExpressionNode @unaryExp)
            return ProcessUnaryNode(@unaryExp);

        else if (expression is TypeCastingExpressionNode @typeCast)
            return $"{ProcessExpression(@typeCast.expression)} as {@typeCast.type}";
        
        else if (expression is ReferenceModifier @refMod)
            return @refMod.ToString();
        
        else if (expression is MethodCallNode @methodCall)
            return @methodCall.ToString();
        
        else if (expression is IdentifierNode @identifier)
            return @identifier.symbol.ToString();
        
        else if (expression is BooleanLiteralNode @bool)
            return @bool.value ? "true" : "false";

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
        return unaryExp.expOperator.value switch
        {
            "+" => ProcessExpression(unaryExp.expression),
            "-" => "-" + ProcessExpression(unaryExp.expression),

            _   => "NaN"
        };
    }

}
