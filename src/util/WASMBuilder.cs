using System.Text;
using Compiler.CodeProcessing.CompilationStructuring;
using static Compiler.Util.Compilation.WASMBuilder.WasmInstructions;

namespace Compiler.Util.Compilation;

public class WASMBuilder
{

    public WasmModule Module {get; private set;} = null!;

    public WasmModule GenerateModule()
    {
        Module ??= new();
        return Module;
    }


    public class WasmModule
    {
        private List<WasmImportedMethod> _imports = [];
        private List<WasmMethod> _methods = [];

        private List<WasmData> _ROData = [];
        private ulong _emptyDataPtr = 4;

        public WasmImportedMethod CreateImport(string methodName, TypeItem returnType, string[] path)
        {
            var import = new WasmImportedMethod(this, methodName, WasmHelper.AbsType2Wasm(returnType), path);
            _imports.Add(import);
            return import;
        }

        public WasmMethod CreateMethod(string methodName, TypeItem returnType)
        {
            var method = new WasmMethod(this, methodName, WasmHelper.AbsType2Wasm(returnType));
            _methods.Add(method);
            return method;
        }

        public ulong AppendReadOnlyData(string label, string value)
        {   
            int dataLen = Encoding.UTF8.GetBytes(value).Length;
            byte[] dataLenInBytes = BitConverter.GetBytes((uint) dataLen);

            string lenBinaryStr = "";

            foreach (var i in dataLenInBytes) lenBinaryStr += $"\\{i:X2}";

            _ROData.Add(new (label, _emptyDataPtr, lenBinaryStr, value));

            var ptr = _emptyDataPtr;
            _emptyDataPtr += (ulong)dataLen + 4;

            return ptr;
        }

        public string ToAssemblyString()
        {
            string str = "(module";
            
            foreach (var import in _imports)
            {
                str += $"\n\t{import.ToAssemblyString()}";
            }

            str += "\n\n\t(memory (export \"mem\") 1)\n";

            if (_imports.Count > 0) str += "\n";

            foreach (var method in _methods)
            {
                var str2 = method.ToAssemblyString().Split('\n');
                foreach (var i in str2) str += $"\t{i}\n";
                str += "\n";
            }

            byte[] numberAsBytes = BitConverter.GetBytes((uint)_emptyDataPtr);
            str += "\t(data (i32.const 0) \"";
            foreach (var i in numberAsBytes) str += $"\\{i:X2}";
            str += "\")\n";

            foreach (var data in _ROData)
            {
                str += $"\t{data.ToAssemblyString()}\n";
            }

            str += ")";

            return str;
        }
    }

    public class WasmImportedMethod(WasmModule module, string name, WasmType returnType, string[] path)
    {
        private WasmModule _moduleRef = module;
        public WasmModule Module => _moduleRef;

        public readonly List<string> path = [.. path];
        public readonly WasmMethod function = new(module, name, returnType, true);

        public string ToAssemblyString()
        {
            var str = "(import";

            str += $" \"{string.Join('.', path[.. ^1])}\"";
            str += $" \"{path[^1]}\"";

            str += $" {function.ToAssemblyString()}";

            str += ")";
            return str;
        }
    }

    public class WasmMethod(WasmModule module, string name, WasmType returnType, bool isImported = false)
    {
        private WasmModule _moduleRef = module;
        public WasmModule Module => _moduleRef;

        private string _name = name;
        private Dictionary<string, WasmType> _parameters = [];
        private List<WasmType> _localVariables = [];

        private List<IWasmInstruction> _instructions = [];
        private List<WasmIf> _ifStack = [];

        private WasmType _returnType = returnType;

        private bool _isImported = isImported;

        public int ParametersLength => _parameters.Count;

        public void AddParameter(string name, TypeItem type)
            => _parameters.Add(name, WasmHelper.AbsType2Wasm(type));

        public int AddLocal(TypeItem type)
        {
            var i = _localVariables.Count;
            _localVariables.Add(WasmHelper.AbsType2Wasm(type));
            return i;
        }
        
        public void Emit(IWasmInstruction instruction)
        {
            if (!_isImported)
            {
                if (_ifStack.Count > 0) _ifStack[^1].Emit(instruction);
                else _instructions.Add(instruction);
            }
        }

        public void EmitIf()
        {
            if (!_isImported)
            {
                var ifblock = new WasmIf();

                Emit(ifblock);

                if (_ifStack.Count > 0 && _ifStack[^1].ElseEnabled)
                    _ifStack.RemoveAt(_ifStack.Count - 1);

                _ifStack.Add(ifblock);
            }
        }

        public void EmitElse()
        {
            if (!_isImported)
            {
               _ifStack[^1].ElseEnabled = true;
            }
        }

        public void EndIf()
        {
            if (!_isImported) _ifStack.RemoveAt(_ifStack.Count - 1);
        }

        public string ToAssemblyString()
        {
            string str = "";
            if (!_isImported) str += $"(export \"{_name}\" (func ${_name}))\n";

            str += $"(func ${_name}";

            if (_parameters.Count > 0)
            foreach (var p in _parameters)
                str += $" (param ${p.Key} {WasmHelper.Type2Str(p.Value)})";

            if (_localVariables.Count > 0)
            foreach (var l in _localVariables)
                str += $" (local {WasmHelper.Type2Str(l)})";
            
            if (_returnType != WasmType._void)
            {
                str += $" (result {WasmHelper.Type2Str(_returnType)})";
                if (_instructions.Count > 0) str += "\n";
            }

            if (!_isImported && _instructions.Count > 0)
                str += '\n';

            foreach (var i in _instructions)
            {
                var lines = i.ToString().Split('\n');
                foreach (var j in lines) str += $"\t{j}\n";
            }

            str += ")";

            return str;
        }
    }

    public class WasmData(string label, ulong adress, params string[] values)
    {
        private string _label = label;
        private ulong _adress = adress;
        private string[] _values = values;

        public string ToAssemblyString()
        {
            string str = $"(data (i32.const {_adress})";

            foreach (var i in _values)
                str += " \"" + i + '"';

            str += ')';

            return str;
        }
    }


    public enum WasmType {
        i32,
        i64,
        f32,
        f64,

        _string,

        _void
    }

    static class WasmHelper
    {
        public static string Type2Str(WasmType type)
            => type switch
            {
                WasmType.i32 => "i32",
                WasmType.i64 => "i64",
                WasmType.f32 => "f32",
                WasmType.f64 => "f64",
                WasmType._void => ";;void",
                WasmType._string => "i64",
                _ => "???"
            };
    
        public static WasmType AbsType2Wasm(TypeItem absType)
        {
            if (absType.Value is PrimitiveType primitive)
            {
                return primitive.Value switch
                {
                    PrimitiveTypeList.Void => WasmType._void,

                    PrimitiveTypeList.Integer_8 or
                    PrimitiveTypeList.Integer_16 or
                    PrimitiveTypeList.Integer_32 or
                    PrimitiveTypeList.UnsignedInteger_8 or
                    PrimitiveTypeList.UnsignedInteger_16 or
                    PrimitiveTypeList.UnsignedInteger_32 => WasmType.i32,

                    PrimitiveTypeList.Integer_64 or
                    PrimitiveTypeList.UnsignedInteger_64  => WasmType.i64,

                    PrimitiveTypeList.Integer_128 or
                    PrimitiveTypeList.UnsignedInteger_128 => WasmType.i64, // unsuported sizes

                    PrimitiveTypeList.Floating_32 => throw new NotImplementedException(),
                    PrimitiveTypeList.Floating_64 => throw new NotImplementedException(),


                    PrimitiveTypeList.Boolean or
                    PrimitiveTypeList.Character => WasmType.i32,

                    PrimitiveTypeList.String => WasmType._string,
                    PrimitiveTypeList.Pointer => throw new NotImplementedException(),
                    PrimitiveTypeList.__Generic__Number => throw new NotImplementedException(),
                
                    _ => throw new NotImplementedException()
                };
            }

            return 0;
        }
    }

    public static class WasmInstructions
    {
        // for polymorfism
        public interface IWasmInstruction
        {
            public string ToString();
        }

        // complex instructions
        public class WasmIf() : IWasmInstruction
        {

            private bool _elseEnabled = false;
            public bool ElseEnabled
            {
                get => _elseEnabled;
                set => _elseEnabled = true;
            }

            private readonly List<IWasmInstruction> _instructions = [];
            private readonly List<IWasmInstruction> _elseInstructions = [];

            public void Emit(IWasmInstruction instruction)
            {
                if (!_elseEnabled)
                    _instructions.Add(instruction);
                else
                    _elseInstructions.Add(instruction);
            }

            public override string ToString()
            {
                string str = "(if (then\n";

                foreach (var i in _instructions)
                    str += $"\t{i}\n";

                if (_elseEnabled)
                {
                    str += ")\n(else\n";
                    foreach (var i in _elseInstructions)
                        str += $"\t{i}\n";
                }

                str += "))";

                return str;
            }

        }


        public readonly struct Comment(string content) : IWasmInstruction
        {
            public readonly string content = content;

            override public string ToString() => $";; {content}";
        }

        public readonly struct Local(LocalMode mode, int index) : IWasmInstruction
        {
            public readonly LocalMode mode = mode;
            public readonly int index = index;

            override public string ToString() => "local." + (mode == LocalMode.get ? "get" : "set") + $" {index}";
        }

        public readonly struct Const(WasmType type, string value) : IWasmInstruction
        {
            public readonly WasmType type = type;
            public readonly string value = value;

            override public string ToString() => $"{WasmHelper.Type2Str(type)}.const {value}";
        }

        public readonly struct Call(string methodName) : IWasmInstruction
        {
            public readonly string target = methodName;

            override public string ToString() => $"call ${target}";
        }

        public readonly struct Return() : IWasmInstruction
        {
            override public string ToString() => $"return";
        }


        // random enums
        public enum LocalMode { get, set }
    }

}
