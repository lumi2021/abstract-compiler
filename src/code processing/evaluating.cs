using System.Security.Cryptography;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;

namespace Compiler.CodeProcessing.Evaluating;


public static class Evaluation
{

    public static CompilationRoot EvalSource(ScriptNode[] scripts)
    {

        var compilation = new CompilationRoot(null!);

        List<NamespaceItem> allNamespaces = [];
        List<MethodItem> allMethods = [];

        // foreach by the scripts 1° layer
        foreach (var script in scripts)
        {
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
                           identifier = new(new PrimitiveTypeItem((p.type as PrimitiveTypeNode)!), [p.identifier])
                        };

                        if (p.type is PrimitiveTypeNode @primitive)
                            parameter.type = new PrimitiveTypeItem(@primitive, @primitive.value, @primitive.isArray);

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
                localVariables.Add(new(mParam.identifier, -(i+1), mParam.type));
            }

            foreach (var l in mt.codeStatements.ToArray())
            {

                if (l is ExpressionNode @exp)
                    EvaluateExpressionIdentifiers(@exp, mt.Parent, localVariables);

                else if (l is VariableDeclarationNode @varDec)
                {
                    var type = new PrimitiveTypeItem(@varDec.type, (@varDec.type as PrimitiveTypeNode)!.value);
                    localVariables.Add(new(@varDec.identifier, localIndex++, type));
                    mt.Alloc(type);
                    var idx = mt.Del(l);

                    if (@varDec.value != null)
                    {

                        var nAssigin = new AssiginmentExpressionNode()
                        {
                            assigne = new IdentifierNode(type, localVariables.Count - 1),
                            value = @varDec.value
                        };
                        mt.InsertRaw(idx, nAssigin);

                    }
                    else
                    {
                        var nAssigin = new AssiginmentExpressionNode()
                        {
                            assigne = new IdentifierNode(type, localVariables.Count - 1),
                            value = DefaultValueOf(@varDec.type)
                        };
                        mt.InsertRaw(idx, nAssigin);
                    }
                }

                else if (l is ReturnStatementNode @return)
                {
                    if (@return.value != null)
                        EvaluateExpressionIdentifiers(@return.value, mt.Parent, localVariables);
                }

                else throw new NotImplementedException(l.ToString());

            }

        }

        Console.WriteLine("__________________________________________________");
        Console.WriteLine("##################### PASS 2 #####################\n");
        Console.WriteLine(compilation);

        // compile into intermediate asm
        foreach (var mt in allMethods)
        {

            foreach (var l in mt.codeStatements.ToArray())
            {

                var instructionsList = StatementToAsm(l);
                foreach (var i in instructionsList)
                    mt.Emit(i);
                mt.compiled = true;

            }

        }

        Console.WriteLine("__________________________________________________");
        Console.WriteLine("##################### PASS 3 #####################\n");
        Console.WriteLine(compilation);

        return compilation;

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

    private static void EvaluateExpressionIdentifiers(ExpressionNode exp, NamespaceItem? ns,  List<LocalVar> decVars)
    {

        if (exp is BinaryExpressionNode @binExp)
        {
            EvaluateExpressionIdentifiers(@binExp.leftStatement, ns, decVars);
            EvaluateExpressionIdentifiers(@binExp.rightStatement, ns, decVars);
        }
        else if (exp is UnaryExpressionNode @unExp)
        {
            EvaluateExpressionIdentifiers(@unExp.expression, ns, decVars);
        }
        
        else if (exp is AssiginmentExpressionNode @ass)
        {
            EvaluateExpressionIdentifiers(@ass.assigne, ns, decVars);
            EvaluateExpressionIdentifiers(@ass.value, ns, decVars);
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
                    @ident.localRef = new(decVars[idx].type, decVars[idx].index);

                }
                else Console.WriteLine("a: " + @ident.symbol);

            }
        }

        else if (exp is MethodCallNode @methodCall)
        {
            if (!@methodCall.processed)
            {

                Identifier methodName = @methodCall.target;
                var method = ns!.methods.Find(m => m.name == methodName);

                if (method != null)
                {
                    @methodCall.target = method.GetGlobalReference();
                    @methodCall.target.refersTo = method;
                    @methodCall.processed = true;
                }

            }
        }

    }

    #endregion

    #region AST to intermadiate Asm

    private static IntermediateInstruction[] StatementToAsm(StatementNode stat)
    {
        
        // Emit an expression
        if (stat is ExpressionNode @exp)
        {
            return ExpressionToAsm(@exp);
        }
        else if (stat is ReturnStatementNode @ret)
        {
            IntermediateInstruction[] value = [];
            if (@ret.value != null)
                value = ExpressionToAsm(@ret.value);
            
            return [.. value, OpCode.Ret()];
        }
        
        else throw new NotImplementedException(stat.GetType().Name);

    }

    private static IntermediateInstruction[] ExpressionToAsm(ExpressionNode exp, TypeItem? expectedType = null)
    {
        List<IntermediateInstruction> rist = [];

        if (exp is BinaryExpressionNode @binaryExp)
        {
            rist.AddRange(ExpressionToAsm(@binaryExp.rightStatement));
            rist.AddRange(ExpressionToAsm(@binaryExp.leftStatement));
            
            rist.Add(@binaryExp.expOperator switch
            {
                "+" => OpCode.Add(),
                "-" => OpCode.Sub(),
                "*" => OpCode.Mul(),
                "/" => OpCode.Div(),
                "%" => OpCode.Rest(),

                _   => throw new NotSupportedException($"operator {@binaryExp.expOperator}")
            });
        }

        else if (exp is IdentifierNode @identfier)
        {
            if (@identfier.isLocal)
            {
                rist.Add(OpCode.GetLocal(@identfier.localRef));
            }
        }

        else if (exp is NumericLiteralNode @numericLit)
        {
            string IlTypeString = "i64";

            if (expectedType != null && expectedType is PrimitiveTypeItem @pt)
                IlTypeString = TypeAsILString(@pt.type);
            
            rist.Add(OpCode.LdConst_int(IlTypeString, (long)@numericLit.value));
        }
            
        else if (exp is StringLiteralNode @stringLit)
        {}
            
        else if (exp is AssiginmentExpressionNode @assigin)
        {
            List<IntermediateInstruction> assigneCode = [];
            TypeItem assiginType = null!;

            // process the assigne first and get the type
            if (@assigin.assigne is IdentifierNode @targetId)
            {
                if (@targetId.isLocal)
                {
                    assigneCode.Add(OpCode.SetLocal(@targetId.localRef));
                    assiginType = @targetId.localRef.refersToType;
                }
            }

            // load the value on stack
            rist.AddRange(ExpressionToAsm(@assigin.value, assiginType));

            // set it into assigne
            rist.AddRange(assigneCode);
        }

        else if (exp is MethodCallNode @mCall)
        {
            if (@mCall.target.refersTo is MethodItem @method)
                rist.Add(OpCode.CallStatic(@method));
        }

        else throw new NotImplementedException(exp.GetType().Name);

        return [.. rist];
    }

    #endregion

    /* temporary structs */
    private struct TempBinaryExp(string op)
    {
        public readonly string op = op;
        public ExpressionNode left;
        public ExpressionNode right;

        public override string ToString() => $"{left} {op} {right}";
    }
    private struct LocalVar(Identifier id, int index, TypeItem type)
    {
        public Identifier identifier = id;
        public TypeItem type = type;
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

        throw new NotImplementedException();
        //return 0;
    }

    public static string TypeAsILString(PrimitiveType t)
    {
        return t switch
        {
            PrimitiveType.Void => "void",
            PrimitiveType.Integer_8 => "i8",
            PrimitiveType.Integer_16 => "i16",
            PrimitiveType.Integer_32 => "i32",
            PrimitiveType.Integer_64 => "i64",
            PrimitiveType.Integer_128 => "i128",
            PrimitiveType.UnsignedInteger_8 => "u8",
            PrimitiveType.UnsignedInteger_16 => "u16",
            PrimitiveType.UnsignedInteger_32 => "u32",
            PrimitiveType.UnsignedInteger_64 => "u64",
            PrimitiveType.UnsignedInteger_128 => "u128",
            PrimitiveType.Floating_32 => "f32",
            PrimitiveType.Floating_64 => "f64",
            PrimitiveType.Boolean => "bool",
            PrimitiveType.Character => "char",
            PrimitiveType.String => "str",

            _ => throw new NotImplementedException($"{t}")
        };
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
