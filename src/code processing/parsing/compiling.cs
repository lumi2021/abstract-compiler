using System.Text.RegularExpressions;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;

namespace Compiler.CodeProcessing.Compiling;

public static class Compilator
{

    static CompilationRoot compRoot = null!;

    public static void Compile(CompilationRoot program, string oPath, string oFile)
    {

        NASMBuilder builder = new();

        compRoot = program;

        WriteAsmFromFile("./resources/libs/stdx32.asm", builder);

        foreach (var i in program.namespaces)
            WriteNamespace(i, builder);

        if (!Directory.Exists(oPath))
            Directory.CreateDirectory(oPath);
        
        File.WriteAllText(oPath + '/' + oFile, builder.Emit());
        
        compRoot = null!;

    }

    private static void WriteNamespace(NamespaceItem nameSpace, NASMBuilder builder)
    {
        if (nameSpace.ScriptSourceReference is HeaderScript) return;

        builder.Comment($"writing {nameSpace.GetGlobalReferenceAsm()}");
        foreach (var method in nameSpace.methods)
            WriteMethod(method, builder);
    }

    private static void WriteMethod(MethodItem method, NASMBuilder builder)
    {

        // write method label
        builder.DefLabel($"{method.GetGlobalReferenceAsm()}");

        // allocate stack memory
        ushort stackSize = (ushort)(Math.Ceiling(method.LocalMemorySize / (double)ArchData.StackAligin) * ArchData.StackAligin);
        ushort argsSize  = (ushort)(Math.Ceiling(method.parameters.Sum(e => e.type.Value.Size) / (double)ArchData.StackAligin)
        * ArchData.StackAligin);

        builder.Enter(stackSize).WithComment($"local size: {method.LocalMemorySize} bytes");

        VirtualStack vStack = new();

        foreach (var i in method.interLang)
        {
            // push stack operations
            if (i.instruction == Instruction.LdConst)
                vStack.Push(StackValueKind.Constant, new(StringAsIlType(i.parameters[0]), TypeDefKind.Value), i.parameters[1]);
            
            else if (i.instruction == Instruction.GetLocal)
            {
                int localIndex = int.Parse(i.parameters[0]);

                if (localIndex >= 0) // is local
                {
                    var idx = localIndex;
                    var type = method.LocalData[idx];

                    int memOffset = 0;
                    for (int p = 0; p < idx && p < method.LocalData.Count; p++)
                        memOffset += method.LocalData[p].Value.Size;

                    vStack.Push(StackValueKind.Local, (PrimitiveType)type.Value, $"{memOffset}");
                }
                else // is argument
                {
                    var idx = Math.Abs(localIndex)-1;
                    var ptype = method.parameters[idx].type;

                    int memOffset = -(ArchData.StackAligin * 2);

                    for (int p = 0; p < idx && p < method.parameters.Count; p++)
                        memOffset -= method.parameters[p].type.Value.Size;

                    vStack.Push(StackValueKind.Local, (PrimitiveType)ptype.Value, $"{memOffset}");
                }
            }
            
            // binary operators
            else if (
                i.instruction == Instruction.Add || i.instruction == Instruction.Sub ||
                i.instruction == Instruction.Mul || i.instruction == Instruction.Div ||
                i.instruction == Instruction.Rest)
            {
                var l = vStack.Pop();
                var r = vStack.Pop();
    
                builder.Mov(ArchData.Acumulator, LoadStackValue(l));
                builder.Add(ArchData.Acumulator, LoadStackValue(r)).WithComment("Operation");

                vStack.Push(StackValueKind.Operation, l.type, $"{ArchData.Acumulator}");
            }

            // assigin operations
            else if (i.instruction == Instruction.SetLocal)
            {
                var v = vStack.Pop();
                var idx = int.Parse(i.parameters[0]);

                var t = (method.LocalData[idx].nodeReference as PrimitiveTypeNode)!.value;

                int memOffset = 0;
                for (var l = 0; l < idx && l < method.LocalData.Count; l++)
                    memOffset += method.LocalData[l].Value.Size;

                Pointer ptr = new(GetAsNasmType(t),$"{ArchData.StackBasePtr} - {memOffset}");

                builder.Mov(ptr, LoadStackValue(v))
                .WithComment($"Set {LoadStackValue(v)} in local {i.parameters[0]}");
            }

            // method calls
            else if (i.instruction == Instruction.CallStatic)
            {
                string[] path = i.parameters[0].Split(':');
                Identifier nspaceId = new(null!, path[0].Split("."));
                Identifier methodId = new(null!, path[1].Split('?')[0].Split("."));

                NamespaceItem nspace = compRoot.namespaces.Find(e => e is ExplicitNamespaceItem @exp && @exp.name == nspaceId)!;
                MethodItem targetMethod = nspace.methods.Find(e => e.name == methodId)!;

                for (int p = targetMethod.parameters.Count - 1; p >= 0 ; p--)
                {
                    var arg = vStack.Pop();
                    builder.Push(LoadStackValue(arg)).WithComment($"loading arg.{p} ({targetMethod.parameters[p].identifier})");
                }

                builder.Call(targetMethod).WithComment("Calling method");

                vStack.Push(StackValueKind.Operation, (PrimitiveType)targetMethod.returnType.Value, $"{ArchData.Acumulator}");
            }

            // returning
            else if (i.instruction == Instruction.Ret)
            {
                if (vStack.Length > 0)
                {
                    var stackVal = vStack.Pop();
                    if (stackVal.kind != StackValueKind.Operation | stackVal.data != ArchData.Acumulator.ToString())
                        builder.Mov(ArchData.Acumulator, LoadStackValue(stackVal))
                        .WithComment("put the value to return into the acumulator");
                } 

                builder.Leave().WithComment("Clear the method stack");

                // clear the arguments
                if (argsSize > 0)
                {
                    builder.Pop(ArchData.Base).WithComment("hold the pointer to return");
                    builder.Add(ArchData.StackPtr, argsSize).WithComment("Clear the parameters");
                    builder.Push(ArchData.Base).WithComment("put the pointer to return back on the top of the stack");
                }

                // return the methods
                builder.Ret().WithComment("Return the method");
            }

            else builder.Comment($"* => {i}");
        }

        builder.LineFeed();

    }

    private static void WriteAsmFromFile(string filePath, NASMBuilder builder)
    {
        string[] source = File.ReadAllText(filePath).Replace("\r\n", "\n").Split('\n');

        string currentSection = "";

        for (var i = 0; i < source.Length; i++)
        {
            var lineSplited = source[i].Split(';');

            var line = lineSplited[0].Trim();
            string comment = lineSplited.Length > 1 ? lineSplited[1].Trim() : "";

            if (line.StartsWith("section"))
            {
                currentSection = line[8 ..].Trim();
                continue;
            }

            if (line.StartsWith("extern "))
                builder.AppendExternLabel(line[7 ..].Trim());

            else if (line.StartsWith("global "))
                builder.AppendGlobalLabel(line[7 ..].Trim());
            
            else if (currentSection == ".text")
            {
                // FIXME regex is a little broke here, change it in the future
                string pattern = @"(?<!\[.*?)([\s,]+)(?![^\[]*?\])";

                var tokens = Regex.Split(line, pattern)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s) && s != ",")
                .ToArray();
            
                if (tokens.Length == 0) continue;

                string label = "";
                string instruction = "";
                string[] args = [];

                if (tokens[^1].EndsWith(':'))
                {
                    label = tokens[^1][.. ^1];
                    tokens = tokens[1 ..];
                }

                if (tokens.Length > 0)
                {
                    
                    instruction = tokens[0];
                    if (tokens.Length > 1) 
                        args = tokens[1 ..];

                }

                builder.AppendInstruction(label, instruction, args).WithComment(comment);

            }

            else if (currentSection == ".data")
            {
                // FIXME regex is a little broke here, change it in the future
                string pattern = @"(?<!\"".*?)([\s,]+)(?![^\[]*?\"")";

                var tokens = Regex.Split(line, pattern)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s) && s != ",")
                .ToArray();
                
                if (tokens.Length == 0) continue;

                string label = "";
                NASMDataSize size = 0!;
                string[] parameters = [];
                
                label = tokens[0];
                
                if (tokens.Length > 3)
                {
                    size = tokens[1] switch
                    {
                        "db" => NASMDataSize.Byte,
                        "dw" => NASMDataSize.Word,
                        "dd" => NASMDataSize.DWord,
                        "dq" => NASMDataSize.QWord,
                        _ => 0
                    };

                    parameters = tokens[2 ..];
                }
                
                builder.DeclarateDynamicDataItem(label, size, parameters);
            }

        }

        builder.LineFeed();

    }


    private static string LoadStackValue(StackValue sv)
    {
        if (sv.kind == StackValueKind.Constant || sv.kind == StackValueKind.Operation)
        {
            if (sv.type.Value == PrimitiveTypeList.Boolean) return sv.data == "true" ? "0x0" : "0x1";
            return sv.data;
        }
        
        else if (sv.kind == StackValueKind.Local)
        {
            var stkOffset = int.Parse(sv.data);
            return NasmH.FromStack(sv.type.Size, stkOffset);
        }

        return "; error";
    }

    private static PrimitiveTypeList StringAsIlType(string value) => value switch {
        "void" => PrimitiveTypeList.Void,

        "i8" => PrimitiveTypeList.Integer_8,
        "i16" => PrimitiveTypeList.Integer_16,
        "i32" => PrimitiveTypeList.Integer_32,
        "i64" => PrimitiveTypeList.Integer_64,
        "i128" => PrimitiveTypeList.Integer_128,

        "u8" => PrimitiveTypeList.UnsignedInteger_8,
        "u16" => PrimitiveTypeList.UnsignedInteger_16,
        "u32" => PrimitiveTypeList.UnsignedInteger_32,
        "u64" => PrimitiveTypeList.UnsignedInteger_64,
        "u128" => PrimitiveTypeList.UnsignedInteger_128,

        "f32" => PrimitiveTypeList.Floating_32,
        "f64" => PrimitiveTypeList.Floating_64,

        "bool" => PrimitiveTypeList.Boolean,
        "char" => PrimitiveTypeList.Character,
        "str" => PrimitiveTypeList.String,

        _ => PrimitiveTypeList.Void
    };

    private static NASMDataSize GetAsNasmType(PrimitiveTypeList p)
        => p switch
        {   
            PrimitiveTypeList.Boolean or
            PrimitiveTypeList.UnsignedInteger_8 or
            PrimitiveTypeList.Integer_8 => NASMDataSize.Byte,

            PrimitiveTypeList.UnsignedInteger_16 or
            PrimitiveTypeList.Integer_16 => NASMDataSize.Word,

            PrimitiveTypeList.Character or
            PrimitiveTypeList.Floating_32 or
            PrimitiveTypeList.UnsignedInteger_32 or
            PrimitiveTypeList.Integer_32 => NASMDataSize.DWord,

            PrimitiveTypeList.Floating_64 or
            PrimitiveTypeList.UnsignedInteger_64 or
            PrimitiveTypeList.Integer_64 => NASMDataSize.QWord,
            
            PrimitiveTypeList.Void or _ => 0
        };

}

static class NasmH
{
    public static string FromStack(int size, int offset) =>
        (size == 1 ? "BYTE" : size == 2 ? "WORD" : size == 4 ? "DWORD" : "QWORD")
        + $"[{ArchData.StackBasePtr} " + (offset >= 0 ? $"- {offset}" : $"+ {Math.Abs(offset)}") + ']';
}
static class ArchData
{

    private static Archtecture _targetArch = Archtecture.x86;
    public static Archtecture Arch { get => _targetArch; set => _targetArch = value; }

    public static int StackAligin => _targetArch switch
    {
        Archtecture.x16 => 2,
        Archtecture.x86 => 4,
        Archtecture.x64 => 16,
        _ => 0
    };

    private static string StdPrefix => _targetArch switch
    {
        Archtecture.x16 => "",
        Archtecture.x86 => "E",
        Archtecture.x64 => "R",
        _ => ""
    };

    // general purpoise
    public static Register Acumulator     => new($"{StdPrefix}AX");
    public static Register Base           => new($"{StdPrefix}BX");
    public static Register Counter        => new($"{StdPrefix}CX");
    public static Register Data           => new($"{StdPrefix}DX");

    public static Register Return         => Acumulator;

    // index registers
    public static Register SourceIdx      => new($"{StdPrefix}SI");
    public static Register DestinationIdx => new($"{StdPrefix}SI");

    // stack
    public static Register StackBasePtr   => new($"{StdPrefix}BP");
    public static Register StackPtr       => new($"{StdPrefix}SP");

}

enum Archtecture
{
    x16,
    x86,
    x64
}


readonly struct VirtualStack()
{
    private readonly List<StackValue> stack = [];

    public void Push(StackValueKind kind, PrimitiveType type, dynamic value) => stack.Add(new(kind, type, value));

    public StackValue Pop()
    {
        var a = stack[^1];
        stack.RemoveAt(stack.Count -1);
        return a;
    }

    public int Length => stack.Count;

}
readonly struct StackValue(StackValueKind kind, PrimitiveType type, string data)
{
    public readonly StackValueKind kind = kind;
    public readonly PrimitiveType type = type;

    public readonly string data = data;
}

enum StackValueKind : byte
{
    Constant,
    Field,
    Local,
    Operation
}
