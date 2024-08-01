using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;

using OpCodes = Compiler.Util.Compilation.NASMBuilder.OpCodes;
using Reg = Compiler.Util.Compilation.NASMBuilder.Register;
using Arg = Compiler.Util.Compilation.NASMBuilder.Argument;
using Idx = Compiler.Util.Compilation.NASMBuilder.Index;

namespace Compiler.CodeProcessing.Compiling;

public class NasmCompiler : BaseCompiler
{

    private CompilationRoot compilation = null!;
    private NASMBuilder builder = null!;
    private VirtualStack vstack = new();
    // kinds:
    //    0 -> literal/constant;
    //    1 -> local variable;
    //    2 -> returned (or in some register);

    public override void Compile(CompilationRoot program, string oPath, string oFile)
    {
        Console.WriteLine("Compiling into Netwide Assembly...");
        compilation = program;
        builder = new();

        // for though scripts
        foreach (var script in program.scripts)
        {
            if (script is HeaderScript @headerScript)
                CompileLibs(headerScript.assemblyPath);
        }

        // for though namespaces
        foreach (var ns in program.namespaces) CompileNamespace(ns);

        Console.Write("Saving ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"\"{oFile}\"");
        Console.ResetColor();
        Console.Write(" in ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"\"{Path.GetFullPath(oPath)}\"");
        Console.ResetColor();
        Console.WriteLine("...");

        if (!Directory.Exists(oPath))
            Directory.CreateDirectory(oPath);
        
        File.WriteAllText($"{oPath}/{oFile}", builder.EmitAssemblyCode());

        builder = null!;
        compilation = null!;
    }

    private void CompileLibs(string libPath)
    {
        string[] assembly = File.ReadAllText(libPath).Replace("\r\n", "\n")
        .Split('\n', StringSplitOptions.TrimEntries);

        Dictionary<string, int> sections = [];
        Dictionary<string, int> code_labels = [];
        Dictionary<string, string> data_labels = [];
        Dictionary<string, NASMBuilder.AsmJumpBridge> jump_bridges = [];

        for (var i = 0; i < assembly.Length; i++)
        {
            var line = assembly[i];

            if (line.StartsWith("extern"))
                builder.AddExterns(line[7 ..].Split(',', StringSplitOptions.TrimEntries));

            else if (line.StartsWith("global"))
                builder.AddGlobals(line[7 ..].Split(',', StringSplitOptions.TrimEntries));

            else if (line.StartsWith("section"))
                sections.Add(line.Split(' ', StringSplitOptions.TrimEntries)[1], i);
        }

        foreach (var section in sections.Where(e => e.Key == ".data" || e.Key == ".rodata"))
        {
            for (var i = section.Value+1; i < assembly.Length; i++)
            {
                if (string.IsNullOrEmpty(assembly[i])) continue;
                if (assembly[i].StartsWith("section")) break;

                var a = assembly[i];

                var line = assembly[i].Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

                string original_label = line[0];
                string size = line[1]; // the data size (db, dw, dd, dq)
                string value = string.Join(" ", line[2..]);

                data_labels.Add(original_label, builder.StoreHardcoded($"{size} {value}"));
            }
        }

        if (sections.TryGetValue(".text", out int text_section))
        {
            for (var i = text_section + 1; i < assembly.Length; i++)
            {
                if (string.IsNullOrEmpty(assembly[i])) continue;
                if (assembly[i].StartsWith("section")) break;

                var line = assembly[i].Split(';')[0].Trim();
                if (line.Length == 0) continue;

                if (line.Contains('?') && line.EndsWith(':'))
                {
                    jump_bridges.Clear();
                    builder.DelcarateMethodLabel(line[..^1]);
                }
                
                else if (line.EndsWith(':'))
                {
                    var label = line[..^1];

                    if (label == ".err")
                    {}

                    NASMBuilder.AsmJumpBridge bridge = null!;
                    if (!jump_bridges.TryGetValue(label, out bridge!))
                    {
                        bridge = new();
                        jump_bridges.Add(label, bridge);
                    }
                    bridge.SetBridge(builder.GetNextInstructionIndex());
                }
                
                else
                {
                    foreach (var j in data_labels)
                        line = line.Replace(j.Key, j.Value);
                    line = line.Replace("__?LINE?__", $"{i+1}");

                    var tokens = line.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToArray();

                    if (((string[])
                    [
                        "JMP", "JE", "JZ", "JNE", "JNZ",
                        "JG", "JNLE", "JGE", "JNL", "JL",
                        "JNGE", "JLE", "JNG"
                    ]
                    ).Contains(tokens[0].ToUpper()) && tokens.Length == 2 && tokens[1].StartsWith('.'))
                    {
                        // is a jump instruction. jumps should be handled in a different way
                        
                        NASMBuilder.AsmJumpBridge bridge = null!;
                        if (!jump_bridges.TryGetValue(tokens[1], out bridge!))
                        {
                            bridge = new();
                            jump_bridges.Add(tokens[1], bridge);
                        }

                        builder.Emit(OpCodes.HardcodedJmp(tokens[0], bridge));
                    }

                    else builder.Emit(OpCodes.Hardcoded(line));
                }
            }
        }
    }

    private void CompileNamespace(NamespaceItem nameSpace)
    {
        foreach (var method in nameSpace.methods)
        {
            if (method.ScriptRef is not HeaderScript)
                CompileMethod(method);
        }
    }

    private void CompileMethod(MethodItem method)
    {
        vstack.Clear();
        builder.DeclarateMethod(method);

        if (method.LocalMemorySize > 0)
            builder.Emit(OpCodes.Enter(method.LocalMemorySize));

        Stack<NASMBuilder.AsmJumpBridge> ifStack = new();

        foreach (var i in method.interLang)
        {
            switch (i.instruction)
            {
                case IntermediateAssembyLanguage.Instruction.Nop:
                    builder.Emit(OpCodes.Nop());
                    break;

                case IntermediateAssembyLanguage.Instruction.LdConst:
                    if (i.parameters[0] == "str")
                    {
                        string lbl = builder.StoreString(i.parameters[1]);
                        vstack.Push((0, "str", lbl));
                    }
                    else
                        vstack.Push((0, i.parameters[0], i.parameters[1]));

                    break;
                
                case IntermediateAssembyLanguage.Instruction.GetLocal:
                    var type = GetLocalDataType(method, int.Parse(i.parameters[0])).ToAsmString();
                    vstack.Push((1, type, i.parameters[0]));
                    break;

                case IntermediateAssembyLanguage.Instruction.SetLocal:

                    int idx = int.Parse(i.parameters[0]);
                    var t = method.LocalData[idx];

                    var data = vstack.Pop();

                    if (idx >= 0)
                    {
                        if (data.kind == 0)
                            builder.Emit(OpCodes.Mov(long.Parse(data.value), GetLocal(method, idx)));

                        if (data.kind == 1) {
                            builder.Emit(OpCodes.Mov(GetLocal(method, int.Parse(data.value)), Reg.Acumulator_x32));
                            builder.Emit(OpCodes.Mov(Reg.Acumulator_x32, GetLocal(method, idx)));
                        }

                        else if (data.kind == 2)
                            builder.Emit(OpCodes.Mov(Reg.Acumulator_x32, GetLocal(method, idx)));
                    }

                    else builder.Emit(OpCodes.Unknown($"SetLocal is, still, not implemented for parameters! sowy! ;3"));

                    break;


                case IntermediateAssembyLanguage.Instruction.CrtArray:

                    int arraySizeInBytes = int.Parse(i.parameters[1]) * GetIlTypeSize(i.parameters[0]);

                    builder.Emit(OpCodes.Push(arraySizeInBytes));
                    builder.Emit(OpCodes.Call("Std.Memory@GenArray?i32"));

                    vstack.Push((2, "arr#" + i.parameters[0], "A"));

                    break;


                case IntermediateAssembyLanguage.Instruction.CallStatic:

                    var referedMethod = FindMethod(i.parameters[1]);
                    if (referedMethod == null) {
                        builder.Emit(OpCodes.Unknown($"method refered as \"{i.parameters[1]}\" was not found!"));
                        break;
                    }

                    int parametersCount = referedMethod.Parameters.Count;

                    if (parametersCount > 0)
                    {
                        builder.Emit(OpCodes.Sub(Reg.StackPointer, referedMethod.ParametersMemorySize));

                        int pbOffset = 0;
                        for (var paramIdx = 0; paramIdx < parametersCount; paramIdx++)
                        {
                            var paramSize = referedMethod.Parameters[paramIdx].type.Value.Size;

                            var stackTop = vstack.Pop();

                            if (stackTop.kind == 0) // CONSTANT
                            {
                                if (stackTop.type.StartsWith('i') || stackTop.type.StartsWith('u'))
                                {
                                    builder.Emit(OpCodes.Mov(long.Parse(stackTop.value),
                                    new Arg(paramSize, pbOffset)));
                                }
                                else if (stackTop.type == "str")
                                {
                                    builder.Emit(OpCodes.Mov(stackTop.value, Reg.Acumulator_x32));
                                    builder.Emit(OpCodes.Mov(Reg.Acumulator_x32,
                                    new Arg(paramSize, pbOffset)));
                                }
                                else builder.Emit(OpCodes.Unknown($"type {stackTop.type} is still not supported! sowy! ;3"));
                            }
                            else if (stackTop.kind == 1) // LOCAL
                            {
                                builder.Emit(OpCodes.Mov(GetLocal(method, int.Parse(stackTop.value)), Reg.Acumulator_x32));
                                builder.Emit(OpCodes.Mov(Reg.Acumulator_x32, new Arg(paramSize, pbOffset)));
                            }
                            else if (stackTop.kind == 2) // Register
                            {
                                builder.Emit(OpCodes.Mov(Reg.Acumulator_x32, new Arg(paramSize, pbOffset)));
                            }

                            pbOffset += paramSize;
                        }
                    }

                    builder.Emit(OpCodes.Call(ConvertIlToNasmLabel(i.parameters[1])));
                    if (i.parameters[0] != "void") vstack.Push((2, i.parameters[0], "A"));

                    break;

                case IntermediateAssembyLanguage.Instruction.Conv:
                    
                    var stackValue = vstack.Pop();

                    if (stackValue.type == "str") {
                        builder.Emit(OpCodes.Sub(Reg.StackPointer, 4));

                        if (stackValue.kind == 1) // LOCAL
                        {
                            builder.Emit(OpCodes.Mov(GetLocal(method, int.Parse(stackValue.value)), Reg.Acumulator_x32));
                            builder.Emit(OpCodes.Mov(Reg.Acumulator_x32, new Arg(4, 0)));
                        }
                        else if (stackValue.kind == 2) // Register
                        {
                            builder.Emit(OpCodes.Mov(Reg.Acumulator_x32, new Arg(4, 0)));
                        }
                        else throw new NotImplementedException();

                        builder.Emit(OpCodes.Call($"Std.Type.Casting@Cast_{i.parameters[0]}?str"));
                        vstack.Push((2, "str", "A"));
                    }
                    else
                    {
                        builder.Emit(OpCodes.Unknown($"Conversions from {stackValue.type} are still not implemented!"));
                        vstack.Push(stackValue);
                    }

                    break;

                case IntermediateAssembyLanguage.Instruction.Add:
                    if (i.parameters[0] == "i64")
                    {
                        var num_a = vstack.Pop();
                        var num_b = vstack.Pop();

                        if (num_a.kind == 0) // CONSTANT
                            builder.Emit(OpCodes.Mov(long.Parse(num_a.value), Reg.Acumulator_x32));

                        else if (num_a.kind == 1) // LOCAL
                            builder.Emit(OpCodes.Mov(GetLocal(method, int.Parse(num_a.value)), Reg.Acumulator_x32));

                        if (num_b.kind == 0) // CONSTANT
                            builder.Emit(OpCodes.Mov(long.Parse(num_b.value), Reg.Base_x32));

                        else if (num_b.kind == 1) // LOCAL
                            builder.Emit(OpCodes.Mov(GetLocal(method, int.Parse(num_b.value)), Reg.Base_x32));
                        

                        builder.Emit(OpCodes.Add(Reg.Acumulator_x32, Reg.Base_x32));

                        if (i.parameters[0] != "void") vstack.Push((2, i.parameters[0], "Accumulator"));

                    }
                    else builder.Emit(OpCodes.Unknown($"Add not implemented to {i.parameters[0]}! sowy! ;3"));

                    break;

                case IntermediateAssembyLanguage.Instruction.If:

                    var jmpBridge = new NASMBuilder.AsmJumpBridge();
                    ifStack.Push(jmpBridge);

                    var stkVal = vstack.Pop();

                    // Check the condition
                    switch (i.parameters[0])
                    {
                        case "True":
                            if (stkVal.kind == 0)
                            {
                                builder.Emit(OpCodes.Mov(stkVal.value == "True" ? 1 : 0, Reg.Acumulator_x32));
                                builder.Emit(OpCodes.Cmp(Reg.Acumulator_x32, 1));
                            }

                            builder.Emit(OpCodes.JumpIfNotEquals(jmpBridge));
                            break;

                        case "False":
                            if (stkVal.kind == 0)
                            {
                                builder.Emit(OpCodes.Mov(stkVal.value == "True" ? 1 : 0, Reg.Acumulator_x32));
                                builder.Emit(OpCodes.Cmp(Reg.Acumulator_x32, 0));
                            }

                            builder.Emit(OpCodes.JumpIfNotEquals(jmpBridge));
                            break;

                        case "Equal":
                            if (stkVal.kind == 0)
                            {
                                builder.Emit(OpCodes.Mov(stkVal.value, Reg.Acumulator_x32));
                                builder.Emit(OpCodes.Cmp(Reg.Acumulator_x32, long.Parse(i.parameters[1])));
                            }
                            else if (stkVal.kind == 1)
                            {
                                builder.Emit(OpCodes.Mov(GetLocal(method, int.Parse(stkVal.value)), Reg.Acumulator_x32));
                                builder.Emit(OpCodes.Cmp(Reg.Acumulator_x32, long.Parse(i.parameters[1])));
                            }

                            builder.Emit(OpCodes.JumpIfNotEquals(jmpBridge));
                            break;

                        case "Greater":
                            if (stkVal.kind == 0)
                            {
                                builder.Emit(OpCodes.Mov(stkVal.value, Reg.Acumulator_x32));
                                builder.Emit(OpCodes.Cmp(Reg.Acumulator_x32, long.Parse(i.parameters[1])));
                            }
                            else if (stkVal.kind == 1)
                            {
                                builder.Emit(OpCodes.Mov(GetLocal(method, int.Parse(stkVal.value)), Reg.Acumulator_x32));
                                builder.Emit(OpCodes.Cmp(Reg.Acumulator_x32, long.Parse(i.parameters[1])));
                            }

                            builder.Emit(OpCodes.JumpIfLessEqual(jmpBridge));
                            break;

                        default:
                            builder.Emit(OpCodes.Unknown($"{i}"));
                            break;
                    }
                    
                    break;

                case IntermediateAssembyLanguage.Instruction.Else:
                    var jmpb = ifStack.Pop();
                    var njmpb = new NASMBuilder.AsmJumpBridge();

                    builder.Emit(OpCodes.Jump(njmpb));
                    jmpb.SetBridge(builder.GetNextInstructionIndex());

                    ifStack.Push(njmpb);

                    break;

                case IntermediateAssembyLanguage.Instruction.EndIf:
                    var a = ifStack.Pop();
                    a.SetBridge(builder.GetNextInstructionIndex());
                    break;


                default:
                    builder.Emit(OpCodes.Unknown($"{i}"));
                    break;
            }
        }

        if (method.LocalMemorySize > 0)
            builder.Emit(OpCodes.Leave());
    }


    #region Helpers

    private MethodItem? FindMethod(string GMI)
    {
        string namespacePath = GMI.Split(':')[0];
        string methodName = GMI.Split(':')[1].Split('?')[0];

        string[] methodParams = GMI.Split(':')[1].Split('?')[1].Split('_');

        var nameSpace =  compilation.FindNamespace(namespacePath)!;
        var methods = nameSpace.methods.Where(e => e.name.ToString() == methodName);

        foreach (var m in methods) {
            bool ok = true;
            for (var i = 0; i < m.Parameters.Count; i++)
            if (m.Parameters[i].type.ToAsmString() != methodParams[i])
            {
                ok = false;
                break;
            }
            if (ok) return m;
        }

        return null;
    }

    private TypeItem GetLocalDataType(MethodItem method, int idx)
    {
        if (idx >= 0)   return method.LocalData[idx];
        else            return method.Parameters[Math.Abs(idx) -1].type;
    }

    private NASMBuilder.LocalVariable GetLocal(MethodItem method, int idx)
    {
        var type = GetLocalDataType(method, idx);

        int offset = 0;

        if (idx >= 0)
        {
            for (var j = 0; j <= idx && j < method.LocalData.Count; j++)
                offset += method.LocalData[j].Value.Size;
        }
        else
        {
            idx = Math.Abs(idx) - 1;

            offset -= 4; // FIXME pointer size
            for (var j = 0; j <= idx && j < method.Parameters.Count; j++)
                offset -= method.Parameters[j].type.Value.Size;
        }
        
        return new(type.Value.Size, offset);
    }

    private string ConvertIlToNasmLabel(string src) => src.Replace(':', '@');

    private int GetIlTypeSize(string type) => type switch {

        "u8" or
        "i8" => 1,

        "u16" or
        "i16" => 2,

        "u32" or
        "i32" => 4,

        "u64" or
        "i64" => 8,

        _ => 0
    };

    #endregion


    class VirtualStack
    {
        private readonly Stack<(byte kind, string type, string value)> _stack = [];

        public void Clear() => _stack.Clear();

        public void Push((byte kind, string type, string value) item)
            => _stack.Push((item.kind, item.type, item.value));
        public (byte kind, string type, string value) Pop() => _stack.Pop();
        public void PopMany(int count)
        {
            for (var i = 0; i < count; i++)
                _stack.Pop();
        }

        public (byte kind, string type, string value) Next(int idx = 1) => _stack.ToArray()[^idx];
    }
}