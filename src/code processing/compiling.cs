using System.Text;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.Evaluating;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;
using Compiler.Util.Compilation;

namespace Compiler.CodeProcessing.Compiling;

public static class Compilator
{

    public static void Compile(CompilationRoot program, string oPath, string oFile)
    {

        NASMBuilder builder = new();

        foreach (var i in program.namespaces)
            WriteNamespace(i, builder);

        if (!Directory.Exists(oPath))
            Directory.CreateDirectory(oPath);
        
        File.WriteAllText(oPath + '/' + oFile, builder.Emit());

    }

    private static void WriteNamespace(NamespaceItem nameSpace, NASMBuilder builder)
    {
        builder.Comment($"writing {nameSpace.GetGlobalReferenceAsString()}");
        foreach (var method in nameSpace.methods)
            WriteMethod(method, builder);
    }

    private static void WriteMethod(MethodItem method, NASMBuilder builder)
    {

        // write method label
        builder.DefLabel($"{method.GetGlobalReferenceAsString()}");

        // allocate stack memory
        uint stackSize = (uint)(Math.Ceiling(method.LocalMemorySize / (double)ArchData.StackAligin) * ArchData.StackAligin);
        if (stackSize > 0) builder.Sub(ArchData.StackPtr, stackSize)
            .WithComment($"{method.LocalMemorySize} bytes");

        VirtualStack vStack = new();

        foreach (var i in method.interLang)
        {
            // push stack operations
            if (i.instruction == Instruction.LdConst)
                vStack.Push(StackValueKind.Constant, StringAsIlType(i.parameters[0]), i.parameters[1]);
            

            else if (i.instruction == Instruction.GetLocal)
            {
                int localIndex = int.Parse(i.parameters[0]);

                if (localIndex >= 0) // is local
                {
                    var ptype = (method.LocalData[localIndex] as PrimitiveTypeItem)!;
                    vStack.Push(StackValueKind.Local, ptype.type, $"{localIndex}");
                }
                else // is argument
                {
                    var idx = Math.Abs(localIndex)-1;
                    var ptype = (method.parameters[idx].type as PrimitiveTypeItem)!;

                    int memOffset = - ArchData.StackAligin - (int)Math.Ceiling(
                        method.LocalMemorySize / (double)ArchData.StackAligin) * ArchData.StackAligin;

                    for (int p = 0; p <= idx && p < method.parameters.Count; p++)
                        memOffset -= Evaluation.SizeOf(method.parameters[p].type);

                    vStack.Push(StackValueKind.Local, ptype.type, $"{memOffset}");
                }
            }
            
            // binary stack operators
            else if (
                i.instruction == Instruction.Add || i.instruction == Instruction.Sub ||
                i.instruction == Instruction.Mul || i.instruction == Instruction.Div ||
                i.instruction == Instruction.Rest)
            {
                var l = vStack.Pop();
                var r = vStack.Pop();
    
                builder.Mov(ArchData.Acumulator, LoadStackValue(l))
                .WithComment("Add operation");
                builder.Add(ArchData.Acumulator, LoadStackValue(r));

                vStack.Push(StackValueKind.Operation, l.type, $"{ArchData.Acumulator}");
            }

            else if (i.instruction == Instruction.Ret)
            {
                if (vStack.Length != 0)
                    builder.Mov(ArchData.Acumulator, LoadStackValue(vStack.Pop()));

                builder.Ret().WithComment($"return something idk");
            }

            else builder.Comment($"{i}");
        }

        builder.LineFeed();

    }


    private static string LoadStackValue(StackValue sv)
    {
        if (sv.kind == StackValueKind.Constant || sv.kind == StackValueKind.Operation)
            return sv.data;
        
        else if (sv.kind == StackValueKind.Local)
        {
            var stkOffset = int.Parse(sv.data);
            return NasmH.FromStack(stkOffset);
        }

        return "; error";
    }

    private static PrimitiveType StringAsIlType(string value) => value switch {
        "void" => PrimitiveType.Void,

        "i8" => PrimitiveType.Integer_8,
        "i16" => PrimitiveType.Integer_16,
        "i32" => PrimitiveType.Integer_32,
        "i64" => PrimitiveType.Integer_64,
        "i128" => PrimitiveType.Integer_128,

        "u8" => PrimitiveType.UnsignedInteger_8,
        "u16" => PrimitiveType.UnsignedInteger_16,
        "u32" => PrimitiveType.UnsignedInteger_32,
        "u64" => PrimitiveType.UnsignedInteger_64,
        "u128" => PrimitiveType.UnsignedInteger_128,

        "f32" => PrimitiveType.Floating_32,
        "f64" => PrimitiveType.Floating_64,

        "bool" => PrimitiveType.Boolean,
        "char" => PrimitiveType.Character,
        "str" => PrimitiveType.String,

        _ => PrimitiveType.Void
    };

}

static class NasmH
{
    public static string FromStack(int offset)
    => $"[{ArchData.StackPtr} " + (offset >= 0 ? $"- {offset}" : $"+ {Math.Abs(offset)}]");

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
