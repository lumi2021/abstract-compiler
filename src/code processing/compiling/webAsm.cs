using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;
using OpCode = Compiler.Util.Compilation.WASMBuilder.WasmInstructions;

namespace Compiler.CodeProcessing.Compiling;

public class WasmCompiler : BaseCompiler
{

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

                    var method = module.CreateMethod(mt.GetGlobalReferenceAsm(), mt.returnType);

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
                method.Emit(new OpCode.Local(OpCode.LocalMode.get, int.Parse(i.parameters[0]) + method.ParametersLength));
                break;

            case Instruction.SetLocal:
                method.Emit(new OpCode.Local(OpCode.LocalMode.set, int.Parse(i.parameters[0]) + method.ParametersLength));
                break;
            
            case Instruction.LdConst:
                string value = i.parameters[1];
                
                if (i.parameters[0] == "bool")
                    value = value == "True" ? "1 ;; true" : "0 ;; false" ;

                method.Emit(new OpCode.Const(String2WasmType(i.parameters[0]), value));
                break;

            case Instruction.CallStatic:
                string[] methodSections = i.parameters[0].Split(':');
                method.Emit(new OpCode.Call(methodSections[0] + '.' + methodSections[1]));
                break;

            case Instruction.Ret:
                method.Emit(new OpCode.Return());
                break;

            case Instruction.If:
                method.EmitIf();
                break;

            case Instruction.EndIf:
                method.EndIf();
                break;
            
            default:
                method.Emit(new OpCode.Comment($"{i}"));
                break;
        }

    }

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

            _ => throw new NotImplementedException(str)
        };
    }

}
