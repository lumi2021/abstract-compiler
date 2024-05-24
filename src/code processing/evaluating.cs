using System.Security.Cryptography;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.CompilationStructuring;

namespace Compiler.CodeProcessing.Evaluating;


public static class Evaluation
{

    public static void EvalSource(ScriptNode script)
    {

        // hehe time of do some mess :)

        //Console.WriteLine(script);

        var compilation = new CompilationRoot(null!);

        List<NamespaceItem> allNamespaces = [];
        List<MethodItem> allMethods = [];

        // foreach by the script 1° layer
        foreach (var i in script.body)
        {
            if (i is NamespaceNode @namespace)
            {
                var nnamespace = new ExplicitNamespaceItem(i, compilation)
                { name = @namespace.name };

                compilation.namespaces.Add(nnamespace);
                allNamespaces.Add(nnamespace);
            }
            else Console.WriteLine("Node type " + i.GetType().Name + " found outside of a namespace!");
        }

        // foreach by found namespaces and load methods and instructions
        foreach (var ns in allNamespaces)
        {

            var namespaceNode = (ns.nodeReference as NamespaceNode)!;

            foreach (var i in namespaceNode.namespaceScope)
            {
                
                if (i is MethodDeclarationNode @method)
                {
                    var methodItem = new MethodItem(i, ns)
                    { name = @method.name };

                    if (@method.returnType is PrimitiveTypeNode @pt)
                        methodItem.returnType = new PrimitiveTypeItem(@pt, @pt.value, @pt.isArray);

                    foreach (var p in @method.parameters)
                    {
                        var parameter = new ParameterItem(p) {
                           identifier = new([p.identifier])
                        };

                        if (p.type is PrimitiveTypeNode @primitive)
                            parameter.type = new PrimitiveTypeItem(p, @primitive.value, @primitive.isArray);

                        methodItem.parameters.Add(parameter);
                    }

                    foreach (var stat in @method.methodScope)
                    {
                        if (stat is AssiginmentExpressionNode @assigin)
                        {
                            if (@assigin.value is BinaryExpressionNode @right)
                            {
                                @assigin.value = ReduceBinaryExp(@right);
                            }
                        }

                        methodItem.AppendRaw(stat);

                    }

                    ns.methods.Add(methodItem);
                    allMethods.Add(methodItem);
                }

            }

        }

        Console.WriteLine("__________________________________________________");
        Console.WriteLine("##################### PASS 1 #####################\n");
        Console.WriteLine(compilation);

        // foreach by methods to verify identifiers
        foreach (var mt in allMethods)
        {
            List<LocalVar> localVariables = [];
            int localIndex = 0;

            for (var i = 0; i < mt.parameters.Count; i++)
            {
                var mParam = mt.parameters[i];
                localVariables.Add(new(mParam.identifier, -(i+1)));
            }

            foreach (var lr in mt.instructions.ToArray())
            {
                
                var l = (lr as BaseInstructionItem)!.nodeReference;

                if (l is ExpressionNode @exp)
                    EvaluateExpressionIdentifiers(@exp, localVariables);

                else if (l is VariableDeclarationNode @varDec)
                {
                    var type = new PrimitiveTypeItem(@varDec.type, (@varDec.type as PrimitiveTypeNode)!.value);
                    localVariables.Add(new(@varDec.identifier, localIndex++));
                    mt.Alloc(type);
                    var idx = mt.Del(lr);

                    if (@varDec.value != null)
                    {

                        var nAssigin = new AssiginmentExpressionNode()
                        {
                            assigne = new IdentifierNode(localVariables.Count - 1),
                            value = @varDec.value
                        };
                        mt.InsertRaw(idx, nAssigin);

                    }
                    else
                    {
                        var nAssigin = new AssiginmentExpressionNode()
                        {
                            assigne = new IdentifierNode(localVariables.Count - 1),
                            value = DefaultValueOf(@varDec.type)
                        };
                        mt.InsertRaw(idx, nAssigin);
                    }
                }

                else if (l is ReturnStatementNode @return)
                {
                    if (@return.value != null)
                        EvaluateExpressionIdentifiers(@return.value, localVariables);
                }

                else throw new NotImplementedException(l.ToString());

            }

        }

        Console.WriteLine("__________________________________________________");
        Console.WriteLine("##################### PASS 2 #####################\n");
        Console.WriteLine(compilation);

    }

    #region helper methods

    private static ExpressionNode ReduceBinaryExp(BinaryExpressionNode exp)
    {
        var bop = new TempBinaryExp(exp.expOperator)
        {
            left = exp.leftStatement,
            right = exp.rightStatement
        };

        if (bop.left is BinaryExpressionNode @leftBin)
            bop.left = ReduceBinaryExp(@leftBin);

        if (bop.right is BinaryExpressionNode @rightBin)
            bop.right = ReduceBinaryExp(@rightBin);
        
        if (bop.left is NumericLiteralNode @lnum && bop.right is NumericLiteralNode @rnum)
        {
            return new NumericLiteralNode() {value = bop.op switch
            {
                "+" => @lnum.value + @rnum.value,
                "-" => @lnum.value - @rnum.value,
                "*" => @lnum.value * @rnum.value,
                "/" => @lnum.value / @rnum.value,
                "%" => @lnum.value % @rnum.value,

                _ => throw new NotImplementedException(),
            }
            };
        }

        else
        {
            exp.leftStatement = bop.left;
            exp.rightStatement = bop.right;
            return exp;
        }
    }

    private static void EvaluateExpressionIdentifiers(ExpressionNode exp, List<LocalVar> decVars)
    {

        if (exp is BinaryExpressionNode @binExp)
        {
            EvaluateExpressionIdentifiers(@binExp.leftStatement, decVars);
            EvaluateExpressionIdentifiers(@binExp.rightStatement, decVars);
        }
        else if (exp is UnaryExpressionNode @unExp)
        {
            EvaluateExpressionIdentifiers(@unExp.expression, decVars);
        }
        
        else if (exp is AssiginmentExpressionNode @ass)
        {
            EvaluateExpressionIdentifiers(@ass.assigne, decVars);
            EvaluateExpressionIdentifiers(@ass.value, decVars);
        }

        else if (exp is IdentifierNode @ident)
        {
            if (!@ident.processed)
            {

                var idx = decVars.FindIndex(e => e.identifier == @ident.symbol);
                if (idx != -1)
                {

                    @ident.processed = true;
                    @ident.isLocal = true;
                    @ident.localRef = new(decVars[idx].index);

                }
                else Console.WriteLine("a: " + @ident.symbol);

            }
        }

    }

    #endregion

    private static void StatementToAsm(StatementNode stat, IEmitInsturction emitable)
    {
        
        // Emit an expression
        if (stat is ExpressionNode)
        {
            
            if (stat is BinaryExpressionNode @binaryExp)
            {
                StatementToAsm(@binaryExp.leftStatement, emitable);
                StatementToAsm(@binaryExp.rightStatement, emitable);
                
                switch (@binaryExp.expOperator)
                {
                    case "+": emitable.Emit(Instruction.Add); break;
                    case "-": emitable.Emit(Instruction.Sub); break;
                    case "*": emitable.Emit(Instruction.Mul); break;
                    case "/": emitable.Emit(Instruction.Div); break;
                    case "%": emitable.Emit(Instruction.Rest); break;

                }
            }

            else if (stat is IdentifierNode @identfier)
                emitable.Emit(Instruction.GetField, @identfier.symbol.ToString());

            else if (stat is NumericLiteralNode @numericLit)
                emitable.Emit(Instruction.LdInt, @numericLit.value.ToString());

            else if (stat is StringLiteralNode @stringLit)
                emitable.Emit(Instruction.LdString, '"' + @stringLit.value + '"');
            
            else if (stat is AssiginmentExpressionNode @assigin)
            {
                StatementToAsm(@assigin.value, emitable);
                emitable.Emit(Instruction.SetLocal, @assigin.assigne.ToString());
            }
            
            else throw new NotImplementedException(stat.GetType().Name);
        
        }
        
        else if (stat is VariableDeclarationNode @varDeclaration)
        {
            throw new NotImplementedException();
        }
        
        else throw new NotImplementedException(stat.GetType().Name);

    }

    /* temporary structs */
    private struct TempBinaryExp(string op)
    {
        public readonly string op = op;
        public ExpressionNode left;
        public ExpressionNode right;

        public override string ToString() => $"{left} {op} {right}";
    }
    private struct LocalVar(Identifier id, int index)
    {
        public Identifier identifier = id;
        public int index = index;
    }

    public static ushort SizeOf(TypeItem type) => SizeOf((type.nodeReference as TypeNode)!);
    public static ushort SizeOf(TypeNode type)
    {
        if (type is PrimitiveTypeNode pt)
        {

            return pt.value switch
            {
                PrimitiveType.Void                  => 0,

                PrimitiveType.Character             or
                PrimitiveType.Boolean               or
                PrimitiveType.Integer_8             or
                PrimitiveType.UnsignedInteger_8     => 1,

                PrimitiveType.Integer_16            or
                PrimitiveType.UnsignedInteger_16    => 2,

                PrimitiveType.Integer_32            or
                PrimitiveType.UnsignedInteger_32    or
                PrimitiveType.Floating_32           => 4,

                PrimitiveType.Integer_64            or
                PrimitiveType.UnsignedInteger_64    or
                PrimitiveType.Floating_64           => 8,

                _ => throw new NotImplementedException()
            };

        }

        return 0;
    }

    public static ExpressionNode DefaultValueOf(TypeNode type)
    {
        if (type is PrimitiveTypeNode pt)
        {

            return pt.value switch
            {
                PrimitiveType.Void                  => throw new NotImplementedException(),
                PrimitiveType.Character             => throw new NotImplementedException(),
                PrimitiveType.Boolean               => throw new NotImplementedException(),

                PrimitiveType.Integer_8             or
                PrimitiveType.UnsignedInteger_8     or
                PrimitiveType.Integer_16            or
                PrimitiveType.UnsignedInteger_16    or
                PrimitiveType.Integer_32            or
                PrimitiveType.UnsignedInteger_32    or
                PrimitiveType.Floating_32           or
                PrimitiveType.Integer_64            or
                PrimitiveType.UnsignedInteger_64    or
                PrimitiveType.Floating_64           => new NumericLiteralNode() { value = 0 },

                _ => throw new NotImplementedException(type.ToString())
            };

        }

        return null!;
    }

}

public enum Instruction : byte
{
    ______________________a,

    Nop,

    SetField,
    SetLocal,

    GetField,
    GetLocal,

    LdInt,
    LdFloat,
    LdBool,
    LdString,

    Add,
    Sub,
    Mul,
    Div,
    Rest,

    Call,
    Ret
}
