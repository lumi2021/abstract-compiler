using System.Text.RegularExpressions;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.CompilationStructuring;

namespace Compiler.CodeProcessing.IntermediateAssembyLanguage;

public static class OpCode
{
    public static IntermediateInstruction Nop() => new(Instruction.Nop, []);

    public static IntermediateInstruction LdConst_int(string type, long value) => new(Instruction.LdConst, [type, value.ToString()]);
    public static IntermediateInstruction LdConst_string(string value) => new(Instruction.LdConst, ["str", value]);
    public static IntermediateInstruction LdConst_float(string type, float value) => new(Instruction.LdConst, [type, value.ToString()]);
    public static IntermediateInstruction LdConst_bool(bool value) => new(Instruction.LdConst, ["bool", value.ToString()]);

    public static IntermediateInstruction SetLocal(int id) => new(Instruction.SetLocal, [id.ToString()]);
    public static IntermediateInstruction SetLocal(LocalRef lr) => new(Instruction.SetLocal, [lr.index.ToString()]);
    public static IntermediateInstruction GetLocal(int id) => new(Instruction.GetLocal, [id.ToString()]);
    public static IntermediateInstruction GetLocal(LocalRef lr) => new(Instruction.GetLocal, [lr.index.ToString()]);

    public static IntermediateInstruction Add() => new(Instruction.Add, []);
    public static IntermediateInstruction Sub() => new(Instruction.Sub, []);
    public static IntermediateInstruction Mul() => new(Instruction.Mul, []);
    public static IntermediateInstruction Div() => new(Instruction.Div, []);
    public static IntermediateInstruction Rest() => new(Instruction.Rest, []);

    public static IntermediateInstruction CallStatic(MethodItem method) => new(Instruction.CallStatic, [method.GetGlobalReferenceIL()]);
    public static IntermediateInstruction Ret() => new(Instruction.Ret, []);
}

public readonly struct IntermediateInstruction(Instruction instruction, string[] parameters)
{
    public readonly Instruction instruction = instruction;
    public readonly string[] parameters = parameters;

    public override string ToString()
    {
        string strInst = instruction switch
        {
            Instruction.Nop             => "nop",

            Instruction.SetField        => "setField.{0}",
            Instruction.SetLocal        => "setLocal.{0}",
            Instruction.GetField        => "getField.{0}",
            Instruction.GetLocal        => "getLocal.{0}",

            Instruction.LdConst         => parameters[0] != "str" ? "ldConst.{0}" : "ldConst.{0} \"{1}\"",

            Instruction.Add             => "add",
            Instruction.Sub             => "sub",
            Instruction.Mul             => "mul",
            Instruction.Div             => "div",
            Instruction.Rest            => "rest",

            Instruction.CallStatic      => "call.Static {0}",
            Instruction.CallInstance    => "call.Instance {0}",
            Instruction.CallVirtual     => "call.Virtual {0}",
            Instruction.Ret             => "ret",

            _ => throw new NotSupportedException(instruction.ToString())
        };

        var vc = GetVariableCount(strInst);

        strInst = string.Format(strInst, parameters[0..vc]);
        return $"{strInst} {string.Join(", ", parameters[vc..])}";
    }

    private static int GetVariableCount(string formatString)
    {
        return Regex.Matches(formatString, @"\{(\d+)\}")
            .Cast<Match>()
            .Select(m => int.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(-1)
            .Max() + 1;
    }
}

public enum Instruction : byte
{
    Nop,

    SetField,
    SetLocal,

    GetField,
    GetLocal,

    LdConst,

    Add,
    Sub,
    Mul,
    Div,
    Rest,

    CallStatic,
    CallInstance,
    CallVirtual,
    Ret
}
