using System.Text;

namespace Compiler.Util.Compilation;

public class NASMBuilder
{

    private readonly List<NasmInstruction> instructions = [];


    public void Comment(string content)
    {
        instructions.Add(new NasmInstruction("", "", content));
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

    public EIResult Mov(Register target, long value)
    {
        instructions.Add(new("", "MOV", target.ToString(), value > 0 ? $"0x{value:X}" : $"{value}"));
        return new(this);
    }
    public EIResult Mov(Register target, string value)
    {
        instructions.Add(new("", "MOV", target.ToString(), value));
        return new(this);
    }

    public EIResult Add(Register left, long right)
    {
        instructions.Add(new("", "ADD", left.ToString(), right > 0 ? $"0x{right:X}" : $"{right}"));
        return new(this);
    }
    public EIResult Add(Register left, string right)
    {
        instructions.Add(new("", "ADD", left.ToString(), right));
        return new(this);
    }
    
    public EIResult Sub(Register left, long right)
    {
        instructions.Add(new("", "SUB", left.ToString(), right > 0 ? $"0x{right:X}" : $"{right}"));
        return new(this);
    }
    public EIResult Sub(Register left, string right)
    {
        instructions.Add(new("", "SUB", left.ToString(), right));
        return new(this);
    }

    public EIResult Ret()
    {
        instructions.Add(new("", "RET", ""));
        return new(this);
    }


    public string Emit()
    {
        StringBuilder code = new();

        foreach (var instruction in instructions)
            code.AppendLine(instruction.ToString());

        return code.ToString();
    }

    readonly struct NasmInstruction
    {
        public readonly string label;
        public readonly string instruction;
        public readonly string[] parameters;
        public readonly string comment;

        public NasmInstruction(string inst, params string[] parameters)
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
            => (label.Trim() != "" ? $"{label.Trim()}:" : "")
            + (instruction.Trim() != "" ? $"\t{instruction,-6}{string.Join(", ", parameters)}" : "")
            .PadRight(comment.Trim() != "" ? 30 : 0)
            + (comment.Trim() != "" ? $"; {comment}" : "");
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

public readonly struct Register(string value)
{
    private readonly string value = value;
    public override string ToString() => value;
}
