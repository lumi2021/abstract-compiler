using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;
using OpCodes = Compiler.Util.Compilation.NASMBuilder.OpCodes;
using Reg = Compiler.Util.Compilation.NASMBuilder.Register;

namespace Compiler.CodeProcessing.Compiling;

public class NasmCompiler : BaseCompiler
{

    private CompilationRoot compilation = null!;
    private NASMBuilder builder = null!;
    private VirtualStack vstack = new();
    // kinds:
    //    0 -> literal/constant;
    //    1 -> local variable;
    //    2 -> returned (in accumulator);

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
        string[] assembly = File.ReadAllText(libPath).Replace(":", ":\n").Replace("\r\n", "\n")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Dictionary<string, int> sections = [];
        Dictionary<string, int> code_labels = [];
        Dictionary<string, string> data_labels = [];

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
                if (assembly[i].StartsWith("section")) break;

                var line = assembly[i].Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

                string original_label = line[0];
                string size = line[1]; // the data size (db, dw, dd, dq)
                string value = string.Join("", line[2..]);

                data_labels.Add(original_label, builder.StoreHardcoded($"{size} {value}"));
            }
        }

        if (sections.TryGetValue(".text", out int text_section))
        {
            for (var i = text_section + 1; i < assembly.Length; i++)
            {
                if (assembly[i].StartsWith("section")) break;

                var line = assembly[i];

                if (line.Contains('?') && line.EndsWith(':'))
                    builder.DelcarateMethodLabel(line[..^1]);
                
                else
                {
                    foreach (var j in data_labels)
                        line = line.Replace(j.Key, j.Value);

                    builder.Emit(OpCodes.Hardcoded(line));
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

        foreach (var i in method.interLang)
        {
            switch (i.instruction)
            {
                case IntermediateAssembyLanguage.Instruction.Nop: break;
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

                case IntermediateAssembyLanguage.Instruction.CallStatic:

                    var referedMethod = FindMethod(i.parameters[1]);
                    if (referedMethod == null) {
                        builder.Emit(OpCodes.Unknown($"method refered as \"{i.parameters[1]}\" was not found!"));
                        break;
                    }

                    int parametersCount = referedMethod.Parameters.Count;

                    //builder.Emit(OpCodes.Unknown($"calling {referedMethod.name} "
                    //+ $"({string.Join(", ", referedMethod.Parameters.Select(p => p.type.ToAsmString()))})"
                    //+ $" -> {referedMethod.returnType.ToAsmString()}"));

                    if (parametersCount > 0)
                    {
                        builder.Emit(OpCodes.Sub(Reg.StackPointer, referedMethod.ParametersMemorySize));

                        int pbOffset = 0;
                        for (var paramIdx = 0; paramIdx < parametersCount; paramIdx++)
                        {
                            var paramSize = referedMethod.Parameters[paramIdx].type.Value.Size;

                            var stackValue = vstack.Pop();
                            if (stackValue.kind == 0) // CONSTANT
                            {
                                if (stackValue.type.StartsWith('i') || stackValue.type.StartsWith('u'))
                                {
                                    builder.Emit(OpCodes.Mov(long.Parse(stackValue.value),
                                    new NASMBuilder.Argument(paramSize, pbOffset)));
                                }
                                else if (stackValue.type == "str")
                                {
                                    builder.Emit(OpCodes.Mov(stackValue.value, Reg.Acumulator_x32));
                                    builder.Emit(OpCodes.Mov(Reg.Acumulator_x32,
                                    new NASMBuilder.Argument(paramSize, pbOffset)));
                                }
                                else builder.Emit(OpCodes.Unknown($"type {stackValue.type} is still not supported! sowy! ;3"));
                            }

                            if (stackValue.kind == 1) // LOCAL
                            {
                                builder.Emit(OpCodes.Mov(GetLocal(method, int.Parse(stackValue.value)), Reg.Acumulator_x32));
                                builder.Emit(OpCodes.Mov(Reg.Acumulator_x32,
                                    new NASMBuilder.Argument(paramSize, pbOffset)));
                            }
                            // ACCUMULATOR

                            pbOffset += paramSize;
                        }
                    }

                    builder.Emit(OpCodes.Call(ConvertIlToNasmLabel(i.parameters[1])));
                    if (i.parameters[0] != "void") vstack.Push((2, i.parameters[0], "Accumulator"));

                    break;

                case IntermediateAssembyLanguage.Instruction.Conv:
                    
                    var stackTop = vstack.Pop();

                    if (stackTop.type == "str") {
                        builder.Emit(OpCodes.Call($"Std.Type.Casting@Cast_{i.parameters[0]}?str"));
                    }
                    else
                    {
                        builder.Emit(OpCodes.Unknown($"Conversions from {stackTop.type} are still not implemented!"));
                        vstack.Push(stackTop);
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
