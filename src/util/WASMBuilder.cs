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
        public List<WasmImportedMethod> _imports = [];
        public List<WasmMethod> _methods = [];

        public WasmImportedMethod CreateImport(string methodName, TypeItem returnType, string[] path)
        {
            var import = new WasmImportedMethod(methodName, WasmHelper.AbsType2Wasm(returnType), path);
            _imports.Add(import);
            return import;
        }

        public WasmMethod CreateMethod(string methodName, TypeItem returnType)
        {
            var method = new WasmMethod(methodName, WasmHelper.AbsType2Wasm(returnType));
            _methods.Add(method);
            return method;
        }

        public string ToAssemblyString()
        {
            string str = "(module\n";

            str += "\n(export \"memory\" (memory $mem))\n";
            
            foreach (var import in _imports)
            {
                str += $"\t{import.ToAssemblyString()}\n";
            }

            if (_imports.Count > 0) str += "\n";

            foreach (var method in _methods)
            {
                var str2 = method.ToAssemblyString().Split('\n');
                foreach (var i in str2) str += $"\t{i}\n";
                str += "\n";
            }

            str += ")";

            return str;
        }
    }

    public class WasmImportedMethod(string name, WasmType returnType, string[] path)
    {
        public readonly List<string> path = [.. path];
        public readonly WasmMethod function = new(name, returnType, true);

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

    public class WasmMethod(string name, WasmType returnType, bool isImported = false)
    {
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
                _instructions.Add(ifblock);
                _ifStack.Add(ifblock);
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

            if (!_isImported) str += '\n';

            foreach (var i in _instructions)
            {
                var lines = i.ToString().Split('\n');
                foreach (var j in lines) str += $"\t{j}\n";
            }

            str += ")";

            return str;
        }
    }

    public enum WasmType {
        i32,
        i64,
        f32,
        f64,

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

                    PrimitiveTypeList.String => throw new NotImplementedException(),
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
        public readonly struct WasmIf() : IWasmInstruction
        {

            private readonly List<IWasmInstruction> _instructions = [];

            public void Emit(IWasmInstruction instruction)
                => _instructions.Add(instruction);

            public override string ToString()
            {
                string str = "(if (then\n";

                foreach (var i in _instructions)
                str += $"\t{i}\n";

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
