using System.Security.Cryptography;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.Exeptions;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;

namespace Compiler.CodeProcessing.Evaluating;


public static class Evaluation
{

    private static List<NamespaceItem> allNamespaces = [];
    private static List<MethodItem> allMethods = [];

    public static CompilationRoot EvalSource(ScriptNode[] scripts)
    {

        allNamespaces = [];
        allMethods = [];

        var compilation = new CompilationRoot(null!);

        // foreach by the scripts 1° layer
        foreach (var script in scripts)
        {
            foreach (var i in script.body)
            {
                if (i is NamespaceNode @namespace)
                {
                    var nnamespace = new ExplicitNamespaceItem(script.SourceReference, i, compilation)
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
                        methodItem.returnType = new TypeItem(@pt);

                    foreach (var p in @method.parameters)
                    {
                        var parameter = new ParameterItem(p) {
                           identifier = new(new TypeItem(p.type), [p.identifier])
                        };

                        if (p.type is PrimitiveTypeNode @primitive)
                            parameter.type = new TypeItem(@primitive);

                        methodItem.parameters.Add(parameter);
                    }

                    foreach (var stat in @method.methodScope)
                    {
                        if (stat is AssiginmentExpressionNode @assigin)
                            @assigin.value = ReduceExpression(@assigin.value);

                        methodItem.AppendRaw(stat);

                    }

                    ns.methods.Add(methodItem);
                    allMethods.Add(methodItem);
                }

            }

        }

        WritePass(compilation, 1);

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
                    var type = new TypeItem(@varDec.type);
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
                            value = Typing.DefaultValueOf(((PrimitiveType)type.Value).Value)
                        };
                        mt.InsertRaw(idx, nAssigin);
                    }
                }

                else if (l is ReturnStatementNode @return)
                {
                    if (@return.value != null)
                        EvaluateExpressionIdentifiers(@return.value, mt.Parent, localVariables);
                }

                else mt.ScriptRef.ThrowError(
                    new InstructionProcessingNotImplementedException()
                );

            }

        }

        // foreach to find basic errors
        foreach (var mt in allMethods)
        {
            foreach (var l in mt.codeStatements.ToArray())
            {
                if (l is AssiginmentExpressionNode @ass)
                {
                    if (@ass.assigne is IdentifierNode @ident)
                    {
                        var referingType = (PrimitiveType)@ident.refersToType.Value;

                        if (@ass.value is NumericLiteralNode @num)
                        {
                            
                            double value = @num.value;

                            // check for mismatch type
                            var a = IsNumericType(referingType);
                            Console.WriteLine(a);

                            // check for overflow
                            if (value < referingType.MinValue)
                            {
                                // FIXME
                                Console.WriteLine($"Warning! the value {value} is lower than the minimum store capacity of an "
                                + $"{referingType}. It will calse an overflow making the resultant value be {referingType.MaxValue}!");
                                @num.value = referingType.MaxValue;
                            }
                            else if (value > referingType.MaxValue)
                            {
                                // FIXME
                                Console.WriteLine($"Warning! the value {value} is greater than the maximum store capacity of an "
                                + $"{referingType}. It will calsing an overflow making the resultant value be {referingType.MinValue}!");
                                @num.value = referingType.MinValue;
                            }
                        }
                        else EvaluateExpressionType(@ass.value);

                    }
                }
            }
        }

        WritePass(compilation, 2);

        // compile into intermediate asm
        foreach (var mt in allMethods)
        {

            foreach (var l in mt.codeStatements.ToArray())
            {
                try {

                    var instructionsList = StatementToAsm(l);

                    foreach (var i in instructionsList)
                        mt.Emit(i);

                }
                catch (BuildException ex) { mt.ScriptRef.ThrowError(ex); }
            }
            mt.compiled = true;

        }

        WritePass(compilation, 3);

        return compilation;

    }

    #region helper methods

    private static ExpressionNode ReduceExpression(ExpressionNode exp)
    {
        if (exp is BinaryExpressionNode @bin)
            return ReduceBinaryExp(@bin);

        else if (exp is MethodCallNode @call)
            return ReduceMethodCall(@call);
        
        else return exp;
    }

    private static ExpressionNode ReduceBinaryExp(BinaryExpressionNode exp)
    {
        var bop = new TempBinaryExp(exp.expOperator)
        {
            left = exp.leftStatement,
            right = exp.rightStatement
        };

        bop.left = ReduceExpression(bop.left);
        bop.right = ReduceExpression(bop.right);
        
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

    private static MethodCallNode ReduceMethodCall(MethodCallNode call)
    {
        for(var i = 0; i < call.parameters.Count; i++)
            call.parameters[i] = ReduceExpression(call.parameters[i]);
        
        return call;
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
                    @ident.symbol.refersToType = decVars[idx].type;

                }
                else Console.WriteLine("a: " + @ident.symbol);

            }
        }

        else if (exp is MethodCallNode @methodCall)
        {
            if (!@methodCall.processed)
            {

                Identifier methodName = @methodCall.target;
                var method = FindReferencedMethodAndOverloads(methodName, ns)[0];

                if (method != null)
                {
                    @methodCall.target = method.GetGlobalReference();
                    @methodCall.target.refersTo = method;

                    foreach (var arg in @methodCall.parameters)
                    {
                        EvaluateExpressionIdentifiers(arg, ns, decVars);
                    }

                    @methodCall.processed = true;
                }
                else Console.WriteLine($"Undefined Method \"{methodName}\"");

            }
        }

        else if (
            exp is StringLiteralNode ||
            exp is BooleanLiteralNode ||
            exp is NumericLiteralNode ||
            exp is NullLiteralNode
        ) return;

        else throw new NotImplementedException($"{exp} ({exp.GetType().Name})");

    }

    private static TypeItem EvaluateExpressionType(ExpressionNode exp)
    {
        Console.WriteLine(exp.GetType().Name);
        return null!;
    }

    private static bool IsNumericType(ILangType t)
    {
        if (t is PrimitiveType @p)
        {
            return @p.Value switch
            {
                PrimitiveTypeList.Integer_8 or
                PrimitiveTypeList.Integer_16 or
                PrimitiveTypeList.Integer_32 or
                PrimitiveTypeList.Integer_64 or
                PrimitiveTypeList.Integer_128 or
                PrimitiveTypeList.UnsignedInteger_8 or
                PrimitiveTypeList.UnsignedInteger_16 or
                PrimitiveTypeList.UnsignedInteger_32 or
                PrimitiveTypeList.UnsignedInteger_64 or
                PrimitiveTypeList.UnsignedInteger_128 or
                PrimitiveTypeList.Floating_32 or
                PrimitiveTypeList.Floating_64 or
                PrimitiveTypeList.__Generic__Number => true,

                _ => false
            };
        }

        return false;
    }

    private static MethodItem[] FindReferencedMethodAndOverloads(Identifier methodReference, NamespaceItem? rootNamespace)
    {
        if (rootNamespace == null) return [];

        if (methodReference.Len == 1)
        {
            // search inside self namespace
            var itens = rootNamespace.methods.Where(e => e.name == methodReference).ToArray();
            if (itens.Length > 0) return itens;

            // seatch inside usings
        }

        // search for a totally qualified name
        var matchingNamespaces = allNamespaces.Where(e =>
            e is ExplicitNamespaceItem exp
            && methodReference.Len > exp.name.Len
            && exp.name == new Identifier(null!, [.. methodReference.values[.. exp.name.Len]]))
        .ToArray();

        if (matchingNamespaces.Length > 0)
        foreach (var ns in matchingNamespaces)
        {
            if (ns is ExplicitNamespaceItem @exp)
            {
                var localIdentfier = new Identifier(methodReference.refersToType, methodReference.values[@exp.name.Len]);

                var itens = ns.methods.Where(e => e.name == localIdentfier).ToArray();
                if (itens.Length > 0) return itens;
            }

            // TODO implecit namespaces

        }


        return [];
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
        
        else if (stat is AssemblyScopeNode @asmScope)
        {
            Console.WriteLine("ignoring asm block lol");
            return [];
        }

        else throw new InstructionProcessingNotImplementedException();

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

            if (expectedType != null && expectedType is TypeItem @pt)
                IlTypeString = @pt.Value.ToIlString();
            
            rist.Add(OpCode.LdConst_int(IlTypeString, (long)@numericLit.value));
        }
            
        else if (exp is StringLiteralNode @stringLit)
        {
            rist.Add(OpCode.LdConst_string(@stringLit.value));
        }
            
        else if (exp is BooleanLiteralNode @bool)
        {
            rist.Add(OpCode.LdConst_bool(@bool.value));
        }

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
            foreach (var i in @mCall.parameters)
                rist.AddRange(ExpressionToAsm(i));

            if (@mCall.target.refersTo is MethodItem @method)
                rist.Add(OpCode.CallStatic(@method));
        }

        else throw new NotImplementedException(exp.GetType().Name);

        return [.. rist];
    }

    #endregion

    #region Evaluator writer

    public static void WritePass(CompilationRoot compilation, int pass)
    {
        if (!Program.Debug_PrintEval) return;

        string outputDir = CodeProcess.OutputDirectory;

        if (!Directory.Exists(outputDir + "/Evaluation/bin/evaluation/"))
            Directory.CreateDirectory(outputDir + "/Evaluation/bin/evaluation/");
        
        File.WriteAllText(outputDir + $"/Evaluation/bin/evaluation/pass-{pass}.txt", compilation.ToString());
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

}
