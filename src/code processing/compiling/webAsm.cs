using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;
using OpCode = Compiler.Util.Compilation.WASMBuilder.WasmInstructions;

namespace Compiler.CodeProcessing.Compiling;

public class WasmCompiler : BaseCompiler
{

    private static readonly VirtualStack vstack = new();

    public override void Compile(CompilationRoot program, string oPath, string oFile)
    {

        Console.WriteLine("Compiling into Web Assembly...");

        // Creating WASM builder object
        var builder = new WASMBuilder();

        // Generate the main module
        var module = builder.GenerateModule();

        CompileProgram(program, module);

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
        
        File.WriteAllText($"{oPath}/{oFile}", builder.Module.ToAssemblyString());

    }

    private void CompileProgram(CompilationRoot program, WASMBuilder.WasmModule module)
    {

        // foreach for each namespace
        foreach (var ns in program.namespaces)
        {
            // foreach for each method in the current namespace
            foreach (var mt in ns.methods)
            {
                
                if (ns.ScriptSourceReference is not HeaderScript)
                {

                    var method = module.CreateMethod(mt.GetGlobalReferenceAsm(), mt.returnType, mt);

                    foreach (var i in mt.parameters) method.AddParameter(i.identifier.ToString(), i.type);
                    foreach (var i in mt.LocalData) method.AddLocal(i);

                    foreach (var i in mt.interLang) CompileIl(i, method);

                }
                else
                {
                    var import = module.CreateImport(mt.GetGlobalReferenceAsm(), mt.returnType, mt.GetGlobalReferenceAsm().Split('.'));
                    foreach (var i in mt.parameters) import.function.AddParameter(i.identifier.ToString(), i.type);
                }
            }
        }

    }

    private static void CompileIl(IntermediateInstruction i, WASMBuilder.WasmMethod method)
    {
        
        switch (i.instruction)
        {
            case Instruction.GetLocal:
                vstack.Push(GetMethodLocalData(method, int.Parse(i.parameters[0])).ToAsmString());
                method.Emit(new OpCode.Local(OpCode.LocalMode.get, int.Parse(i.parameters[0]) + method.ParametersLength));
                break;

            case Instruction.SetLocal:
                var tSetLc = vstack.Pop();
                method.Emit(new OpCode.Local(OpCode.LocalMode.set, int.Parse(i.parameters[0]) + method.ParametersLength));
                break;
            
            case Instruction.LdConst:
                vstack.Push(i.parameters[0]);
                if (i.parameters[0] == "str")
                {
                    ulong ptr = method.Module.AppendReadOnlyData("string", i.parameters[1]);
                    method.Emit(new OpCode.Const(WASMBuilder.WasmType.i64, $"{ptr}"));
                    method.Emit(new OpCode.Call("Std.Memory.LoadString?str"));
                }
                else
                {
                    string value = i.parameters[1];
                    
                    if (i.parameters[0] == "bool")
                        value = value == "True" ? "1 ;; true" : "0 ;; false" ;

                    method.Emit(new OpCode.Const(String2WasmType(i.parameters[0]), value));
                }
                break;

            case Instruction.CallStatic:
                string[] methodSections = i.parameters[1].Split(':');

                vstack.Pop(methodSections[1].Split('?')[1].Split('_', StringSplitOptions.RemoveEmptyEntries).Length);

                method.Emit(new OpCode.Call(methodSections[0] + '.' + methodSections[1]));
                vstack.Push(i.parameters[0]);
                break;

            case Instruction.Ret:
                vstack.Pop();
                method.Emit(new OpCode.Return());
                vstack.Clear();
                break;

            case Instruction.Conv:
                var tConv = vstack.Pop();

                if (tConv == "str" || i.parameters[0] == "str")
                    method.Emit(new OpCode.Call($"Std.Type.Cast_{i.parameters[0]}?{tConv}"));
                
                else
                {
                    string aSize = tConv[1..];
                    string bSize = i.parameters[0][1..];

                    if ((aSize == "8" || aSize == "16") && bSize == "32") break;

                    else if ((aSize == "8" || aSize == "16" || aSize == "32") && bSize == "64")
                        method.Emit(new OpCode.CastNumericUp(WASMBuilder.WasmType.i32, WASMBuilder.WasmType.i64, i.parameters[0][1]=='i'));
                    
                    else if ((bSize == "8" || bSize == "16" || bSize == "32") && aSize == "64")
                        method.Emit(new OpCode.CastNumericDown(WASMBuilder.WasmType.i64, WASMBuilder.WasmType.i32));
                }


                vstack.Push(i.parameters[0]);
                break;

            case Instruction.If:
                method.EmitIf();
                break;

            case Instruction.Else:
                method.EmitElse();
                break;

            case Instruction.EndIf:
                method.EndIf();
                break;
            
            default:
                method.Emit(new OpCode.Comment($"{i}"));
                break;
        }

    }

    private static TypeItem GetMethodLocalData(WASMBuilder.WasmMethod method, int index)
        => method.src.LocalData[index];
    
    private static WASMBuilder.WasmType String2WasmType(string str)
    {
        return str switch
        {
            "i8" or "u8" or
            "i16" or "u16" or
            "i32" or "i32" => WASMBuilder.WasmType.i32,

            "i64" or "u64" or
            "i128" or "u128" => WASMBuilder.WasmType.i64,

            "f32" => WASMBuilder.WasmType.f32,
            "f64" => WASMBuilder.WasmType.f64,

            "bool" or "char" => WASMBuilder.WasmType.i32,

            "str" => WASMBuilder.WasmType._string,

            _ => throw new NotImplementedException(str)
        };
    }


    class VirtualStack
    {

        public readonly List<string> stack = [];

        public int Length => stack.Count;
        
        public void Clear() => stack.Clear();

        public void Push(string type)
        {
            if (type == "void") return;

            //Console.WriteLine("pushed");
            stack.Add(type);
            //Console.WriteLine(this);
        }
        public string Pop(int count = 1)
        {
            if (count == 0) return "";

            //Console.WriteLine("poped " + count);
            var a = stack[^1];

            for (int i = 0; i < count; i++)
            {
                a = stack[^1];
                stack.RemoveAt(Length - 1);
            }

            //Console.WriteLine(this);

            return a;
        }

        public override string ToString() => $"###\n{string.Join('\n', stack)}\n###";
    }

}
