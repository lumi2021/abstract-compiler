using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.Exeptions;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;
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

                    if (!compilation.scripts.Contains(script.SourceReference))
                        compilation.scripts.Add(script.SourceReference);
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

                        methodItem.Parameters.Add(parameter);
                    }

                    foreach (var stat in @method.methodScope)
                    {
                        methodItem.AppendRaw(TryReduceStatement(stat));
                    }

                    VerifyMethod(methodItem);

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

            for (var i = 0; i < mt.Parameters.Count; i++)
            {
                var mParam = mt.Parameters[i];
                localVariables.Add(new(mParam.identifier, -(i+1), mParam.type));
            }

            foreach (var l in mt.codeStatements.ToArray())
            {
                try {
                    EvaluateStatementIdentifiers(l, mt, localVariables, ref localIndex);
                }
                catch (BuildException ex) {
                    mt.ScriptRef.ThrowError(ex);
                }
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
                            
                            long value = @num.value;

                            // check for mismatch type
                            IsNumericIntegerType(referingType);

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

                        if (@ass.value is FloatingLiteralNode @flt)
                        {
                            
                            double value = @flt.value;

                            // check for mismatch type
                            IsNumericFloatingType(referingType);

                        }

                        else EvaluateExpressionType(@ass.value);

                    }
                }
            }
        }

        // foreach to evaluate types and type casting
        foreach (var mt in allMethods)
        {
            foreach (var l in mt.codeStatements.ToArray())
            {

                if (l is ExpressionNode @expNode)
                {

                    if (l is AssiginmentExpressionNode @assigin)
                    {
                        var t1 = EvaluateExpressionType(@assigin.assigne);
                        var t2 = EvaluateExpressionType(@assigin.value);

                        Console.WriteLine($"{t1} <- {t2}");

                        if (t1 != t2)
                        {
                            if (t2.IsAssignableTo(t1))
                            {
                                @assigin.value = new TypeCastingExpressionNode()
                                {
                                    expression = @assigin.value,
                                    type = (t1.nodeReference as TypeNode)!
                                };
                            }
                            
                            else mt.ScriptRef.ThrowError(
                                new InvalidImplicitCastException(t2, t1));
                        }
                    }

                    else if (l is MethodCallNode @call)
                    {
                        for (var i = 0; i < @call.arguments.Count; i++)
                        {
                            var arg = @call.arguments[i];

                            var t1 = EvaluateExpressionType(arg);
                            var t2 = (@call.target.refersTo as MethodItem)!.Parameters[i].type;

                            if (t1 != t2)
                            {
                                if (t1.IsAssignableTo(t2))
                                {
                                    @call.arguments[i] = new TypeCastingExpressionNode()
                                    {
                                        expression = arg,
                                        type = (t2.nodeReference as TypeNode)!
                                    };
                                }
                                
                                else mt.ScriptRef.ThrowError(
                                    new InvalidImplicitCastException(t1, t2));
                            }
                        }
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

    private static void VerifyMethod(MethodItem method)
    {
        var nameSpace = method.Parent;

        var overloads = nameSpace.methods.Where(m => m.name == method.name).ToArray();
        if (overloads.Length > 0)
        {

            foreach (var i in overloads)
            {
                bool allSame = true;
                if (i.Parameters.Count == method.Parameters.Count)
                {

                    for (var j = 0; j < i.Parameters.Count; j++)
                    {

                        var p1 = i.Parameters[j]; 
                        var p2 = method.Parameters[j]; 

                        if (p1.type != p2.type)
                        {
                            allSame = false;
                            break;
                        }
                    }
                }
                if (allSame)
                {
                    nameSpace.ScriptSourceReference.ThrowError(new InvalidMethodOverloadingException());
                    break;
                }
            }

        }

        return;
    }

    private static StatementNode TryReduceStatement(StatementNode stat)
    {

        if (stat is ExpressionNode @exp)
        {
            return ReduceExpression(@exp);
        }

        else if (stat is ReturnStatementNode @ret)
        {
            if (@ret.value != null)
                @ret.value = ReduceExpression(@ret.value);
            return @ret;
        }

        else if (stat is VariableDeclarationNode @varDec)
        {
            if (@varDec.value != null)
                @varDec.value = ReduceExpression(@varDec.value);
            return @varDec;
        }

        else if (stat is IfStatementNode @ifstat)
        {
            @ifstat.condition = ReduceExpression(@ifstat.condition);
            if (@ifstat.result != null) @ifstat.result = TryReduceStatement(@ifstat.result);

            return @ifstat;
        }

        else Console.WriteLine($"unimplemented \"{stat}\";");

        return stat;
    }

    private static ExpressionNode ReduceExpression(ExpressionNode exp)
    {
        if (exp is BinaryExpressionNode @bin)
            return ReduceBinaryExp(@bin);

        else if (exp is MethodCallNode @call)
            return ReduceMethodCall(@call);
        
        else if (exp is AssiginmentExpressionNode @ass)
        {
            @ass.assigne = ReduceExpression(@ass.assigne);
            @ass.value = ReduceExpression(@ass.value);
            return @ass;
        }
        
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
        for(var i = 0; i < call.arguments.Count; i++)
            call.arguments[i] = ReduceExpression(call.arguments[i]);
        
        return call;
    }

    private static void EvaluateStatementIdentifiers(StatementNode stat, MethodItem mt, List<LocalVar> localVariables, ref int localIndex)
    {
        if (stat is ExpressionNode @exp)
            EvaluateExpressionIdentifiers(@exp, mt, mt.Parent, localVariables);

        else if (stat is VariableDeclarationNode @varDec)
        {
            if (@varDec.value != null) EvaluateExpressionIdentifiers(@varDec.value, mt, mt.Parent, localVariables);
            
            var type = new TypeItem(@varDec.type);
            localVariables.Add(new(@varDec.identifier, localIndex++, type));
            mt.Alloc(type);
            var idx = mt.Del(stat);

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

        else if (stat is ReturnStatementNode @return)
        {
            if (@return.value != null)
                EvaluateExpressionIdentifiers(@return.value, mt, mt.Parent, localVariables);
        }

        else if (stat is IfStatementNode @ifstat)
        {
            EvaluateExpressionIdentifiers(@ifstat.condition, mt, mt.Parent, localVariables);
            if (@ifstat.result != null)
                EvaluateStatementIdentifiers(@ifstat.result, mt, localVariables, ref localIndex);
            
            if (@ifstat.elseStatement != null)
                EvaluateElseIdentifiers(@ifstat.elseStatement, mt, localVariables, ref localIndex);
        }

        else mt.ScriptRef.ThrowError(
            new InstructionProcessingNotImplementedException(stat.GetType().Name)
        );

    }

    private static void EvaluateExpressionIdentifiers(ExpressionNode exp, MethodItem? mt, NamespaceItem? ns,  List<LocalVar> decVars)
    {

        if (exp is BinaryExpressionNode @binExp)
        {
            EvaluateExpressionIdentifiers(@binExp.leftStatement, mt, ns, decVars);
            EvaluateExpressionIdentifiers(@binExp.rightStatement, mt, ns, decVars);
        }
        else if (exp is UnaryExpressionNode @unExp)
        {
            EvaluateExpressionIdentifiers(@unExp.expression, mt, ns, decVars);
        }
        
        else if (exp is TypeCastingExpressionNode @tCast)
        {
            EvaluateExpressionIdentifiers(@tCast.expression, mt, ns, decVars);
        }

        else if (exp is AssiginmentExpressionNode @ass)
        {
            EvaluateExpressionIdentifiers(@ass.assigne, mt, ns, decVars);
            EvaluateExpressionIdentifiers(@ass.value, mt, ns, decVars);
        }

        else if (exp is IdentifierNode @ident)
        {

            var idx = decVars.FindIndex(e => e.identifier == @ident.symbol);

            @ident.processed = true;

            if (idx >= 0)
            {
                @ident.isLocal = true;
                @ident.localRef = new(decVars[idx].type, decVars[idx].index);
                @ident.symbol.refersToType = decVars[idx].type;

            }
            else throw new LocalVariableNotFoundException();
        }

        else if (exp is MethodCallNode @methodCall)
        {
            if (!@methodCall.processed)
            {

                // process arguments
                for (var i = 0; i < @methodCall.arguments.Count; i++)
                    EvaluateExpressionIdentifiers(@methodCall.arguments[i], mt, ns, decVars);
                
                // evaluate arguments types
                Identifier methodName = @methodCall.target;
                var overloads = FindReferencedMethodAndOverloads(methodName, ns);

                if (overloads.Length == 0) throw new MethodNotFoundException();

                // find overload
                MethodItem method = null!;
                foreach (var mtd in overloads)
                {
                    if (@methodCall.arguments.Count != mtd.Parameters.Count) continue;

                    // just to make sure hehe
                    if (@methodCall.arguments.Count == 0 && mtd.Parameters.Count == 0)
                    {
                        method = mtd;
                        break;
                    }

                    for (var i = 0; i < mtd.Parameters.Count; i++)
                    {

                        var pType = mtd.Parameters[i].type;
                        var aType = EvaluateExpressionType(@methodCall.arguments[i]);

                        if (pType == aType)
                        {
                            method = mtd;
                            break;
                        }
                    }
                }

                if (method == null)
                foreach (var mtd in overloads)
                {
                    if (@methodCall.arguments.Count != mtd.Parameters.Count) continue;

                    for (var i = 0; i < mtd.Parameters.Count; i++)
                    {

                        var pType = mtd.Parameters[i].type;
                        var aType = EvaluateExpressionType(@methodCall.arguments[i]);

                        if (aType.IsAssignableTo(pType))
                        {
                            method = mtd;
                            break;
                        }
                    }
                }

                if (method == null) mt!.ScriptRef.ThrowError(new MethodOverloadNotFoundException());

                if (method != null)
                {
                    @methodCall.target = new(method.returnType, method.GetGlobalPath()) {
                        refersTo = method
                    };

                    foreach (var arg in @methodCall.arguments)
                    {
                        EvaluateExpressionIdentifiers(arg, mt, ns, decVars);
                    }

                    @methodCall.processed = true;
                }
                else Console.WriteLine($"l. 442: Undefined Method \"{methodName}\"");

            }
        }

        else if (
            exp is StringLiteralNode ||
            exp is BooleanLiteralNode ||
            exp is NumericLiteralNode ||
            exp is FloatingLiteralNode ||
            exp is NullLiteralNode
        ) return;

        else throw new NotImplementedException($"{exp} ({exp.GetType().Name})");

    }

    private static void EvaluateElseIdentifiers(ElseStatementNode elseStat, MethodItem? mt,  List<LocalVar> decVars, ref int localIndex)
    {
        if (elseStat.condition != null)
            EvaluateExpressionIdentifiers(elseStat.condition, mt, mt?.Parent, decVars);

        if (elseStat.result != null)
            EvaluateStatementIdentifiers(elseStat.result, mt!, decVars, ref localIndex);

        if (@elseStat.elseStatement != null)
                EvaluateElseIdentifiers(@elseStat.elseStatement, mt, decVars, ref localIndex);
    }

    private static TypeItem EvaluateExpressionType(ExpressionNode exp)
    {
        Console.WriteLine($"{exp} ({exp.GetType().Name})");

        if (exp is MethodCallNode @call)
            return @call.target.refersToType;
        
        else if (exp is StringLiteralNode) return new TypeItem(PrimitiveTypeList.String);
        else if (exp is NumericLiteralNode) return new TypeItem(PrimitiveTypeList.__Generic__Number);
        else if (exp is FloatingLiteralNode) return new TypeItem(PrimitiveTypeList.__Generic__Floating);

        else if (exp is IdentifierNode @ident) return @ident.refersToType;

        else if (exp is TypeCastingExpressionNode @tCast) return new TypeItem(@tCast.type);

        else if (exp is BinaryExpressionNode @binary)
        {
            var a = EvaluateExpressionType(@binary.leftStatement);
            var b = EvaluateExpressionType(@binary.rightStatement);

            if (a == b) return a;
            else
            {
                if (a.Value is PrimitiveType @p1 && b.Value is PrimitiveType @p2)
                {
                    return GetMinimumCommomType(p1, p2);
                }
            }
        }

        throw new NotImplementedException($"{exp.GetType().Name} is still not supported.");
    }
    private static TypeItem GetMinimumCommomType(PrimitiveType t1, PrimitiveType t2)
    {
        if (t1.Value == t2.Value) return new(t1.Value);

        if (t1.MaxValue > t2.MaxValue && t1.MinValue < t2.MinValue) return new(t1.Value);
        if (t2.MaxValue > t1.MaxValue && t2.MinValue < t1.MinValue) return new(t2.Value);

        return new(PrimitiveTypeList.Void);
    }

    private static bool IsNumericIntegerType(ILangType t)
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
                PrimitiveTypeList.__Generic__Number => true,

                _ => false
            };
        }

        return false;
    }
    private static bool IsNumericFloatingType(ILangType t)
    {
        if (t is PrimitiveType @p)
        {
            return @p.Value switch
            {
                PrimitiveTypeList.Floating_32 or
                PrimitiveTypeList.Floating_64 or
                PrimitiveTypeList.__Generic__Floating => true,

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

        Console.WriteLine($"\"{methodReference}\" not found!");

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

        else if (stat is IfStatementNode @ifstat)
        {
            return IfToAsm(@ifstat);
        }

        else if (stat is ElseStatementNode @elsestat)
        {
            return ElseToAsm(@elsestat);
        }

        else throw new InstructionProcessingNotImplementedException(stat.GetType().Name);
    }

    private static IntermediateInstruction[] ExpressionToAsm(ExpressionNode exp, TypeItem? expectedType = null)
    {
        List<IntermediateInstruction> rist = [];

        if (exp is BinaryExpressionNode @binaryExp)
        {
            var left = @binaryExp.leftStatement;
            var right = @binaryExp.rightStatement;

            var t1 = EvaluateExpressionType(left);
            var t2 = EvaluateExpressionType(right);
            var p1 = (PrimitiveType)t1.Value;
            var p2 = (PrimitiveType)t2.Value;

            var commonType = GetMinimumCommomType(p1, p2);

            rist.AddRange(ExpressionToAsm(right));
            if (t1 != commonType) rist.Add(OpCode.Conv(commonType.ToAsmString()));
            
            rist.AddRange(ExpressionToAsm(left));
            if (t2 != commonType) rist.Add(OpCode.Conv(commonType.ToAsmString()));
            
            rist.Add(@binaryExp.expOperator switch
            {
                "+" => OpCode.Add(commonType.ToAsmString()),
                "-" => OpCode.Sub(commonType.ToAsmString()),
                "*" => OpCode.Mul(commonType.ToAsmString()),
                "/" => OpCode.Div(commonType.ToAsmString()),
                "%" => OpCode.Rem(commonType.ToAsmString()),

                "==" => OpCode.Equals(),
                "!=" => OpCode.Unequals(),

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
            string IlTypeString = expectedType?.Value.ToIlString() ?? "int";

            if (expectedType is not null and TypeItem @pt)
                IlTypeString = @pt.Value.ToIlString();
            
            rist.Add(OpCode.LdConst_int(IlTypeString, @numericLit.value));
        }

        else if (exp is FloatingLiteralNode @floatLit)
        {
            string IlTypeString = expectedType?.Value.ToIlString() ?? "f64";

            if (expectedType is not null and TypeItem @pt)
                IlTypeString = @pt.Value.ToIlString();
            
            rist.Add(OpCode.LdConst_float(IlTypeString, @floatLit.value));
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
            if (assigin.assigne is IdentifierNode @targetId)
            {
                if (@targetId.isLocal)
                {
                    assigneCode.Add(OpCode.SetLocal(@targetId.localRef));
                    assiginType = @targetId.localRef.refersToType;
                }
            }

            Console.WriteLine(assiginType);

            // load the value on stack
            rist.AddRange(ExpressionToAsm(assigin.value, assiginType));

            // set it into assigne
            rist.AddRange(assigneCode);
        }

        else if (exp is MethodCallNode @mCall)
        {
            foreach (var i in @mCall.arguments)
                rist.AddRange(ExpressionToAsm(i));

            if (@mCall.target.refersTo is MethodItem @method)
                rist.Add(OpCode.CallStatic(@method));
        }

        else if (exp is TypeCastingExpressionNode @tCast)
        {
            var typeToConvert = new TypeItem(@tCast.type);

            rist.AddRange(ExpressionToAsm(@tCast.expression, typeToConvert));
            if (expectedType! != typeToConvert)
            {
                string IlTypeString = new TypeItem(@tCast.type).Value.ToIlString();
                rist.Add(OpCode.Conv(IlTypeString));
            }
        }

        else throw new NotImplementedException(exp.GetType().Name);

        return [.. rist];
    }

    private static IntermediateInstruction[] IfToAsm(IfStatementNode ifstat)
    {
        List<IntermediateInstruction> instructions = [];

        // Get the condition
        instructions.AddRange(ExpressionToAsm(ifstat.condition));

        // Calculate the conditional
        instructions.Add(OpCode.If(ConditionMethod.True));

        // Append scope content
        instructions.AddRange(StatementToAsm(ifstat.result!));

        if (ifstat.elseStatement != null)
            instructions.AddRange(ElseToAsm(ifstat.elseStatement));

        instructions.Add(OpCode.EndIf());

        return [.. instructions];
    }

    private static IntermediateInstruction[] ElseToAsm(ElseStatementNode elseStat)
    {
        List<IntermediateInstruction> instructions = [];

        instructions.Add(OpCode.Else());

        // Get the condition and calculate the conditional
        if (elseStat.condition != null)
        {
            instructions.AddRange(ExpressionToAsm(elseStat.condition));
            instructions.Add(OpCode.If(ConditionMethod.True));
        }

        // Append scope content
        instructions.AddRange(StatementToAsm(elseStat.result!));

        if (elseStat.elseStatement != null)
            instructions.AddRange(ElseToAsm(elseStat.elseStatement));

        return [.. instructions];
    }

    #endregion

    #region Evaluator writer

    public static void WritePass(CompilationRoot compilation, int pass)
    {
        if (!Program.Debug_PrintEval) return;

        string outputDir = CodeProcess.OutputDirectory;

        if (!Directory.Exists(outputDir + "/Evaluation/"))
            Directory.CreateDirectory(outputDir + "/Evaluation/");
        
        File.WriteAllText(outputDir + $"/Evaluation/pass-{pass}.txt", compilation.ToString());
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
