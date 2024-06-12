using System.Text;
using Compiler.CodeProcessing.CompilationStructuring;

namespace Compiler.Util.Compilation;

// TODO rewrite this

[Obsolete]
public class NASMBuilder
{

    private readonly List<string> globals = [];
    private readonly List<string> externs = [];

    private readonly List<NasmInstruction> instructions = [];

    private readonly List<NasmData> dynamicData = [];
    private readonly List<NasmData> readonlyData = [];

    public void AppendExternLabel(string label) => externs.Add(label);
    public void AppendGlobalLabel(string label) => globals.Add(label);

    public void Comment(string content)
    {
        instructions.Add(new("", "", content));
    }
    public void LineFeed(int count = 1)
    {
        for (var i = 0; i < count; i++) instructions.Add(new NasmInstruction("", "", ""));
    }

    public EIResult DefLabel(string label, string comment = "")
    {
        instructions.Add(new(label, "", comment));
        return new(this);
    }

    public EIResult Enter(int size)
    {
        instructions.Add(new("", "ENTER", [$"0x{size:X}", "0x0"]));
        return new(this);
    }
    public EIResult Leave()
    {
        instructions.Add(new("", "LEAVE", ""));
        return new(this);
    }

    public EIResult Mov(Pointer target, long value)
    {
        instructions.Add(new("", "MOV", [target.ToString(), value > 0 ? $"0x{value:X}" : $"{value}"]));
        return new(this);
    }
    public EIResult Mov(Pointer target, string value)
    {
        instructions.Add(new("", "MOV", [target.ToString(), value]));
        return new(this);
    }
    public EIResult Mov(Register target, long value)
    {
        instructions.Add(new("", "MOV", [target.ToString(), value > 0 ? $"0x{value:X}" : $"{value}"]));
        return new(this);
    }
    public EIResult Mov(Register target, string value)
    {
        instructions.Add(new("", "MOV", [target.ToString(), value]));
        return new(this);
    }

    public EIResult Add(Register left, long right)
    {
        instructions.Add(new("", "ADD", [left.ToString(), right > 0 ? $"0x{right:X}" : $"{right}"]));
        return new(this);
    }
    public EIResult Add(Register left, string right)
    {
        instructions.Add(new("", "ADD", [left.ToString(), right]));
        return new(this);
    }
    
    public EIResult Sub(Register left, long right)
    {
        instructions.Add(new("", "SUB", [left.ToString(), right > 0 ? $"0x{right:X}" : $"{right}"]));
        return new(this);
    }
    public EIResult Sub(Register left, string right)
    {
        instructions.Add(new("", "SUB", [left.ToString(), right]));
        return new(this);
    }

    public EIResult Mul(Register left, long right)
    {
        instructions.Add(new("", "MUL", [left.ToString(), right > 0 ? $"0x{right:X}" : $"{right}"]));
        return new(this);
    }
    public EIResult Mul(Register left, string right)
    {
        instructions.Add(new("", "MUL", [left.ToString(), right]));
        return new(this);
    }
    public EIResult IMul(Register left, long right)
    {
        instructions.Add(new("", "IMUL", [left.ToString(), right > 0 ? $"0x{right:X}" : $"{right}"]));
        return new(this);
    }
    public EIResult IMul(Register left, string right)
    {
        instructions.Add(new("", "IMUL", [left.ToString(), right]));
        return new(this);
    }

    public EIResult Div(long right)
    {
        instructions.Add(new("", "DIV", [right > 0 ? $"0x{right:X}" : $"{right}"]));
        return new(this);
    }
    public EIResult Div(string right)
    {
        instructions.Add(new("", "DIV", [right]));
        return new(this);
    }
    public EIResult IDiv(long right)
    {
        instructions.Add(new("", "IDIV", [right > 0 ? $"0x{right:X}" : $"{right}"]));
        return new(this);
    }
    public EIResult IDiv(string right)
    {
        instructions.Add(new("", "IDIV", [right]));
        return new(this);
    }

    public EIResult Xor(Register left, Register right)
    {
        instructions.Add(new("", "XOR", [left.ToString(), right.ToString()]));
        return new(this);
    }
    public EIResult Xor(Register left, string right)
    {
        instructions.Add(new("", "XOR", [left.ToString(), right]));
        return new(this);
    }

    public EIResult ClearRegister(Register target)
    {
        instructions.Add(new("", "XOR", [target.ToString(), target.ToString()]));
        return new(this);
    }


    public EIResult Pop(Register to)
    {
        instructions.Add(new("", "POP", [to.ToString()]));
        return new(this);
    }
    public EIResult Push(Register from)
    {
        instructions.Add(new("", "PUSH", [from.ToString()]));
        return new(this);
    }
    public EIResult Push(string from)
    {
        instructions.Add(new("", "PUSH", [from]));
        return new(this);
    }

    public EIResult Call(string label)
    {
        instructions.Add(new("", "CALL", [label]));
        return new(this);
    }
    public EIResult Call(MethodItem method)
    {
        instructions.Add(new("", "CALL", [method.GetGlobalReferenceAsm()]));
        return new(this);
    }

    public EIResult Cmp(Register val1, Register val2)
    {
        instructions.Add(new("", "CMP", [val1.ToString(), val2.ToString()]));
        return new(this);
    }
    public EIResult Cmp(Register val1, string val2)
    {
        instructions.Add(new("", "CMP", [val1.ToString(), val2]));
        return new(this);
    }

    public EIResult Jmp(string to)
    {
        instructions.Add(new("", "JMP", [to]));
        return new(this);
    }
    public EIResult Jz(string to)
    {
        instructions.Add(new("", "JZ", [to]));
        return new(this);
    }
    public EIResult Je(string to)
    {
        instructions.Add(new("", "JE", [to]));
        return new(this);
    }
    public EIResult Jnz(string to)
    {
        instructions.Add(new("", "JNZ", [to]));
        return new(this);
    }
    public EIResult Jne(string to)
    {
        instructions.Add(new("", "JNE", [to]));
        return new(this);
    }
    public EIResult Jg(string to)
    {
        instructions.Add(new("", "JG", [to]));
        return new(this);
    }
    public EIResult Jge(string to)
    {
        instructions.Add(new("", "JGE", [to]));
        return new(this);
    }
    public EIResult Jl(string to)
    {
        instructions.Add(new("", "JL", [to]));
        return new(this);
    }
    public EIResult Jle(string to)
    {
        instructions.Add(new("", "JLE", [to]));
        return new(this);
    }

    public EIResult Ret()
    {
        instructions.Add(new("", "RET", ""));
        return new(this);
    }

    public EIResult AppendInstruction(string label, string inst, string[]? parameters)
    {
        instructions.Add(new(label, inst.ToUpper(), parameters ?? []));
        return new(this);
    }


    public void DeclarateDynamicDataItem(string label, NASMDataSize size, params string[] data)
        => dynamicData.Add(new(label, size, data));
    public void DeclarateReadonlycDataItem(string label, NASMDataSize size, params string[] data)
        => readonlyData.Add(new(label, size, data));

    public void DeclarateDataLabel(string label)
        => readonlyData.Add(new(label));


    public string Emit()
    {
        StringBuilder code = new();

        if (globals.Count > 0) code.AppendLine($"global {string.Join(", ", globals)}");
        if (externs.Count > 0) code.AppendLine($"extern {string.Join(", ", externs)}");

        if (globals.Count > 0 || externs.Count > 0)
            code.AppendLine();

        code.AppendLine("section .text");
        code.AppendLine("    _main: jmp MyProgram.Main?");
        code.AppendLine();

        foreach (var instruction in instructions)
            code.AppendLine(instruction.ToString());
        
        code.AppendLine();
        
        if (dynamicData.Count > 0)
        {
            code.AppendLine("section .data");

            foreach (var data in dynamicData)
                code.AppendLine(data.ToString());
            
            code.AppendLine();
        }

        if (readonlyData.Count > 0)
        {
            code.AppendLine("section .rodata");

            foreach (var data in readonlyData)
                code.AppendLine(data.ToString());
            
            code.AppendLine();
        }
        
        return code.ToString();
    }

    readonly struct NasmInstruction
    {
        public readonly string label;
        public readonly string instruction;
        public readonly string[] parameters;
        public readonly string comment;

        public NasmInstruction(string inst, string[] parameters)
        {
            label = "";
            instruction = inst;
            this.parameters = parameters;
            comment = "";
        }
        public NasmInstruction(string label, string inst, params string[] parameters)
        {
            this.label = label;
            instruction = inst;
            this.parameters = parameters;
            comment = "";
        }
        public NasmInstruction(string label, string inst, string[] parameters, string comment)
        {
            this.label = label;
            instruction = inst;
            this.parameters = parameters;
            this.comment = comment;
        }
        
        public NasmInstruction(string label, string inst, string comment)
        {
            this.label = label;
            instruction = inst;
            parameters = [];
            this.comment = comment;
        }
        public NasmInstruction(NasmInstruction origin, string comment)
        {
            label = origin.label;
            instruction = origin.instruction;
            parameters = origin.parameters;
            this.comment = comment;
        }

        public override string ToString()
        {
            var line = "";

            if (label.Trim() != "")
                line += label.Trim() + ':';
            
            if (instruction.Trim() != "")
            {
                line = line.PadRight(8);
                line += instruction.Trim().ToUpper();

                if (parameters.Length > 0)
                {
                    line = line.PadRight(16);
                    line += ' ' + string.Join(", ", parameters);
                }

            }

            if (comment.Trim() != "")
            {
                line = line.PadRight(48);
                line += "; " + comment.Trim();
            }

            return line;
        }
    }
    readonly struct NasmData (string label, NASMDataSize size = 0!, params string[] data)
    {
        public readonly string label = label;
        public readonly bool onlyLabel = size == 0;
        public readonly NASMDataSize size = size;
        public readonly string[] data = data;

        public override string ToString()
        {
            var line = "".PadRight(8);

            if (label.Trim() != "")
                line += label.Trim();

            if (!onlyLabel)
            {

                line = line.PadRight(31) + ' ';
                line += size switch {
                    NASMDataSize.Byte  => "db",
                    NASMDataSize.Word  => "dw",
                    NASMDataSize.DWord => "dd",
                    NASMDataSize.QWord => "dq",
                    _ => "db"
                };

                line += "  ";

                if (data.Length > 0)
                    line += string.Join(", ", data);
                else line += "0";

            }

            return line;
        }
    }

    public readonly struct EIResult(NASMBuilder builder)
    {
        private readonly NASMBuilder _builder = builder;

        public void WithComment(string comment)
        {
            var instruction = _builder.instructions[^1];
            _builder.instructions.RemoveAt(_builder.instructions.Count - 1);
            _builder.instructions.Add(new(instruction, comment));
        }

    }

}

public readonly struct Register(string value, NASMDataSize size)
{
    public readonly NASMDataSize size = size;
    private readonly string value = value;
    public override string ToString() => value;
}
public readonly struct Pointer(NASMDataSize size, string value)
{
    private readonly string value = value;
    private readonly string size = size switch {
        NASMDataSize.Byte => "BYTE", NASMDataSize.Word => "WORD", NASMDataSize.DWord => "DWORD",
        NASMDataSize.QWord => "QWORD", _ => ""};

    public override string ToString() => $"{size}[{value}]";
}

public enum NASMDataSize : byte
{
    Byte = 1,
    Word = 2,
    DWord = 4,
    QWord = 8
}
