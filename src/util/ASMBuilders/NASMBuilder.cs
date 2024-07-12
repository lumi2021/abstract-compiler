using System.Text;
using Compiler.CodeProcessing.CompilationStructuring;

namespace Compiler.Util.Compilation;

public class NASMBuilder
{

    private List<string> _globals = [];
    private List<string> _externs = [];

    private List<AsmMethod> _methods = [];

    private List<IAsmData> _dynamicData = [];
    private ulong _roDataCount = 0;
    private List<IAsmData> _readonlyData = [];
    //private ulong _dyDataCount = 0;

    public void AddGlobals(params string[] label) => _globals.AddRange(label);
    public void AddExterns(params string[] label) => _externs.AddRange(label);

    public void DeclarateMethod(MethodItem from)
    {
        string methodLabel = ConvertIlToNasmLabel(from.GetGlobalReference());
        var method = new AsmMethod(methodLabel);
        _methods.Add(method);
    }
    public void DelcarateMethodLabel(string label)
    {
        _methods.Add(new(label));
    }

    public void Emit(IAsmInstruction instruction)
    {
        _methods[^1].AppendInstruction(instruction);
    }

    public string StoreString(string content)
    {
        string label = $"rod_{_roDataCount:X8}";
        _readonlyData.Add(new AsmStringData(label, content));
        _roDataCount++;
        return label;
    }
    public string StoreHardcoded(string value)
    {
        string label = $"rod_{_roDataCount:X8}";
        _readonlyData.Add(new AsmHardcodedData(label, value));
        _roDataCount++;
        return label;
    }

    public uint GetNextInstructionIndex() => (uint)_methods[^1].Instructions.LongLength;

    public string EmitAssemblyCode()
    {
        StringBuilder builder = new();

        if (_globals.Count > 0)
            builder.AppendLine($"global {string.Join(", ", _globals)}");
        if (_externs.Count > 0)
            builder.AppendLine($"extern {string.Join(", ", _externs)}");

        if (_globals.Count > 0 || _externs.Count > 0) builder.AppendLine();
        
        // entry point
        builder.AppendLine("_main: JMP MyProgram@Main?\n");

        builder.AppendLine("section .text");
        foreach (var method in _methods)
        {
            builder.AppendLine($"{method.label}:");

            if (method.Instructions.Length > 0)
            {
                uint count = 0;
                foreach (var instruction in method.Instructions)
                {
                    builder.Append($".L{count:X4}:".PadRight(12));
                    builder.Append(instruction.ToAsmStruction().PadRight(8));
                    builder.Append(instruction.ToAsmParameter());
                    builder.AppendLine();
                    count++;
                }

                builder.AppendLine();
            }
        }

        builder.AppendLine("\nsection .data");

        builder.AppendLine("\nsection .rodata");
        for (var i = 0; i < _readonlyData.Count; i++)
        {

            builder.Append($"rod_{i:X8}".PadRight(24));
            builder.AppendLine(_readonlyData[i].ToAsmString());

        }

        builder.AppendLine("\nsection .bss");

        return builder.ToString();
    }

    #region helpers
    private string ConvertIlToNasmLabel(string src)
    {
        return src.Replace(':', '@');
    }
    #endregion

    public class AsmJumpBridge
    {
        private uint _target = 0;
        public void SetBridge(uint target) => _target = target;
        public override string ToString() => $".L{_target:X4}";
    }

    public class AsmMethod(string label)
    {
        public readonly string label = label;

        private List<AsmJumpBridge> _jmpBridges = [];
        private List<IAsmInstruction> _instructions = [];

        public IAsmInstruction[] Instructions => [.. _instructions];

        public void AppendInstruction(IAsmInstruction instruction) => _instructions.Add(instruction);
    }

    private interface IAsmData
    {
        public string ToAsmString();
    }
    
    public class AsmStringData(string lbl, string content) : IAsmData
    {
        public readonly string label = lbl;
        private string _content = content;

        public string ToAsmString()
        {
            string length = "";
            string content = "";

            byte[] contentData = Encoding.UTF8.GetBytes(_content);
            length = string.Join(',', BitConverter.GetBytes(contentData.Length + 1).Select(e => $"{e:D3}"));

            bool onString = false;
            for (var charIdx = 0; charIdx < _content.Length; charIdx++)
            {
                char c = _content[charIdx];

                if (char.IsAscii(c))
                {
                    if (!onString) {
                        content += charIdx > 0 ? ", \"" : "\"";
                        onString = true;
                    }
                    content += c;
                }
                else
                {
                    if (onString)
                    {
                        onString = false;
                        content += "\", ";
                    }
                    var bts = Encoding.UTF8.GetBytes(c.ToString());
                    content += string.Join(", ", bts.Select(e => $"0x{e:X2}"));
                }
            }

            if (onString) content += "\"";

            return $"db {length}, {content}, 0";
        }

    }
    public class AsmHardcodedData(string lbl, string value) : IAsmData
    {
        public readonly string label = lbl;
        private string _value = value;

        public string ToAsmString() => _value;

    }

    public static class OpCodes
    {
        public static IAsmInstruction Nop()                                  => new Op_Nop();

        public static IAsmInstruction Enter(uint size)                       => new Op_Enter(size);
        public static IAsmInstruction Leave()                                => new Op_Leave();

        public static IAsmInstruction Call(string method)                    => new Op_Call(method);

        public static IAsmInstruction Mov(long value, Register register)     => new Op_Mov_Const_To_Reg(register, value);
        public static IAsmInstruction Mov(long value, LocalVariable local)   => new Op_Mov_Const_To_Local(local, value);
        public static IAsmInstruction Mov(LocalVariable local, Register reg) => new Op_Mov_Local_To_Reg(reg, local);

        public static IAsmInstruction Mov(Register reg, Argument arg)        => new Op_Mov_Reg_To_Arg(arg, reg);
         public static IAsmInstruction Mov(long value, Argument arg)         => new Op_Mov_Const_To_Arg(arg, value);

        public static IAsmInstruction Mov(Register reg, LocalVariable local) => new Op_Mov_Reg_To_Local(local, reg);

        public static IAsmInstruction Mov(string str, Register reg)          => new Op_Mov_String_To_Reg(reg, str);

        public static IAsmInstruction Add(Register reg, long value)          => new Op_Add_Reg_Const(reg, value);
        public static IAsmInstruction Add(Register reg, LocalVariable value) => new Op_Add_Reg_Local(reg, value);
        public static IAsmInstruction Add(Register rega, Register regb)      => new Op_Add_Reg_Reg(rega, regb);

        public static IAsmInstruction Sub(Register reg, long value)          => new Op_Sub_Reg_Const(reg, value);

        public static IAsmInstruction Hardcoded(string content)              => new Op_Hardcoded(content);
        public static IAsmInstruction HardcodedJmp
        (string opCode, AsmJumpBridge bridge)                                => new Op_HardcodedJmp(opCode, bridge);

        public static IAsmInstruction Unknown(string text)                   => new Op_Unknown(text);

        #region OpCodes structures

        private readonly struct Op_Nop : IAsmInstruction
        {
            public string ToAsmStruction() => "NOP";
            public string ToAsmParameter() => $"";
        }

        private readonly struct Op_Enter(uint size) : IAsmInstruction
        {
            private readonly uint _size = size;

            public string ToAsmStruction() => "ENTER";
            public string ToAsmParameter() => $"0x{_size:X2}, 0";
        }
        private readonly struct Op_Leave() : IAsmInstruction
        {
            public string ToAsmStruction() => "LEAVE";
            public string ToAsmParameter() => $"";
        }
        
        private readonly struct Op_Call(string target) : IAsmInstruction
        {
            private readonly string _target = target;

            public string ToAsmStruction() => "CALL";
            public string ToAsmParameter() => _target;
        }
        private readonly struct Op_Jmp(AsmJumpBridge bridge) : IAsmInstruction
        {
            private readonly AsmJumpBridge _to = bridge;

            public string ToAsmStruction() => "JMP";
            public string ToAsmParameter() => $"{_to}";
        }

        #region MOV
        private readonly struct Op_Mov_Const_To_Reg(Register reg, long constant) : IAsmInstruction
        {
            private readonly Register _to = reg;
            private readonly long _from = constant;

            public string ToAsmStruction() => "MOV";
            public string ToAsmParameter() => $"{Reg2String(_to)}, 0x{_from:X}";
        }
        private readonly struct Op_Mov_Const_To_Local(LocalVariable localRelative, long constant) : IAsmInstruction
        {
            private readonly LocalVariable _to = localRelative;
            private readonly long _from = constant;

            public string ToAsmStruction() => "MOV";
            public string ToAsmParameter() => $"{_to}, 0x{_from:X}";
        }
        private readonly struct Op_Mov_Local_To_Reg(Register reg, LocalVariable localRelative) : IAsmInstruction
        {
            private readonly Register _to = reg;
            private readonly LocalVariable _from = localRelative;

            public string ToAsmStruction() => "MOV";
            public string ToAsmParameter() => $"{Reg2String(_to)}, {_from}";
        }
        private readonly struct Op_Mov_Reg_To_Local(LocalVariable localRelative, Register reg) : IAsmInstruction
        {
            private readonly LocalVariable _to = localRelative;
            private readonly Register _from = reg;

            public string ToAsmStruction() => "MOV";
            public string ToAsmParameter() => $"{_to}, {Reg2String(_from)}";
        }
        
        private readonly struct Op_Mov_Const_To_Arg(Argument arg, long constant) : IAsmInstruction
        {
            private readonly Argument _to = arg;
            private readonly long _from = constant;

            public string ToAsmStruction() => "MOV";
            public string ToAsmParameter() => $"{_to}, 0x{_from:X}";
        }
        private readonly struct Op_Mov_Reg_To_Arg(Argument arg, Register reg) : IAsmInstruction
        {
            private readonly Argument _to = arg;
            private readonly Register _from = reg;

            public string ToAsmStruction() => "MOV";
            public string ToAsmParameter() => $"{_to}, {Reg2String(_from)}";
        }
        
        private readonly struct Op_Mov_String_To_Reg(Register reg, string str) : IAsmInstruction
        {
            private readonly Register _to = reg;
            private readonly string _from = str;

            public string ToAsmStruction() => "MOV";
            public string ToAsmParameter() => $"{Reg2String(_to)}, {_from}";
        }
        #endregion

        private readonly struct Op_Add_Reg_Const(Register reg, long constant) : IAsmInstruction
        {
            private readonly Register _reg = reg;
            private readonly long _const = constant;

            public string ToAsmStruction() => "ADD";
            public string ToAsmParameter() => $"{Reg2String(_reg)}, 0x{_const:X}";
        }
        private readonly struct Op_Add_Reg_Local(Register reg, LocalVariable localRelative) : IAsmInstruction
        {
            private readonly Register _reg = reg;
            private readonly LocalVariable _offset = localRelative;

            public string ToAsmStruction() => "ADD";
            public string ToAsmParameter() => $"{Reg2String(_reg)}, {_offset}";
        }
        private readonly struct Op_Add_Reg_Reg(Register reg1, Register reg2) : IAsmInstruction
        {
            private readonly Register _reg1 = reg1;
            private readonly Register _reg2 = reg2;

            public string ToAsmStruction() => "ADD";
            public string ToAsmParameter() => $"{Reg2String(_reg1)}, {Reg2String(_reg2)}";
        }
        
        private readonly struct Op_Sub_Reg_Const(Register reg, long constant) : IAsmInstruction
        {
            private readonly Register _reg = reg;
            private readonly long _value = constant;

            public string ToAsmStruction() => "SUB";
            public string ToAsmParameter() => $"{Reg2String(_reg)}, 0x{_value:X}";
        }

        private readonly struct Op_Hardcoded : IAsmInstruction
        {

            private readonly string _opcode;
            private readonly string _args;

            public Op_Hardcoded(string content)
            {
                int fsi = content.IndexOf(' ');

                _opcode = content[..(fsi >= 0 ? fsi : content.Length)].ToUpper().Trim();
                _args = (fsi >= 0
                    ? _args = content[fsi..].Trim()
                    :   _args = ""
                );
            }

            public string ToAsmStruction() => _opcode;
            public string ToAsmParameter() => _args;
        }
        private readonly struct Op_HardcodedJmp(string opcode, AsmJumpBridge bridge) : IAsmInstruction
        {
            private readonly string _opcode = opcode;
            private readonly AsmJumpBridge _bridge = bridge;

            public string ToAsmStruction() => _opcode.ToUpper();
            public string ToAsmParameter() => $"{_bridge}";
        }

        private readonly struct Op_Unknown(string text) : IAsmInstruction
        {
            private readonly string _text = text;

            public string ToAsmStruction() => ";";
            public string ToAsmParameter() => _text;
        }
        
        #endregion
    }

    public enum NASMDataSize : byte
    {
        Byte = 1,
        Word = 2,
        DWord = 4,
        QWord = 8
    }

    public interface IAsmInstruction
    {
        public string ToAsmStruction();
        public string ToAsmParameter();
    }

    public enum Register
    {
        Acumulator_x64, Acumulator_x32, Acumulator_x16,
        Base_x64, Base_x32, Base_x16,
        Count_x64, Count_x32, Count_x16,
        Data_x64, Data_x32, Data_x16,

        R8_x64, R8_x32, R8_x16,
        R9_x64, R9_x32, R9_x16,
        R10_x64, R10_x32, R10_x16,
        R11_x64, R11_x32, R11_x16,
        R12_x64, R12_x32, R12_x16,
        R13_x64, R13_x32, R13_x16,
        R14_x64, R14_x32, R14_x16,
        R15_x64, R15_x32, R15_x16,
        
        Instruction,
        StackPointer,
        BasePointer,

        SourceIndex,
        DestinationIndex,
    }

    public readonly struct LocalVariable (int size, int offset)
    {
        private readonly int _byteOffset = offset;
        private readonly NASMDataSize _size = (NASMDataSize)size;
        public override string ToString()
            => $"{_size.ToString().ToUpper()}[EBP " + (_byteOffset > 0 ? "-" : "+") + $" {_byteOffset}]";
    }
    public readonly struct Argument (int size, int paramOff)
    {
        private readonly int _byteOffset = paramOff;
        private readonly NASMDataSize _size = (NASMDataSize)size;
        public override string ToString() => $"{_size.ToString().ToUpper()}[ESP + {_byteOffset}]";
    }

    private static string Reg2String(Register reg) => reg switch
    {
        Register.Acumulator_x64 => "RAX",
        Register.Acumulator_x32 => "EAX",
        Register.Acumulator_x16 => "AX",
        Register.Base_x64 => "RBX",
        Register.Base_x32 => "EBX",
        Register.Base_x16 => "BX",
        Register.Count_x64 => "RCX",
        Register.Count_x32 => "ECX",
        Register.Count_x16 => "CX",
        Register.Data_x64 => "RDX",
        Register.Data_x32 => "EDX",
        Register.Data_x16 => "DX",
        Register.Instruction => "EIP",
        Register.StackPointer => "ESP",
        Register.BasePointer => "EBP",
        Register.SourceIndex => "",
        Register.DestinationIndex => "",
        _ => ""
    };

}
