using System.Text;

namespace Compiler.CodeProcessing;


public abstract class NodeSyntax
{

    protected NodeSyntax? _parent = null;
    protected List<NodeSyntax> _childrenNodes = [];

    public NodeSyntax? Parent => _parent;
    public NodeSyntax[] ChildrenNodes => [.. _childrenNodes];

    public void AddChildren(params NodeSyntax[] nodes)
    {
        _childrenNodes.AddRange(nodes);
        foreach (var i in nodes) i._parent = this;
    }

    public override string ToString() => GetType().Name + ";";

}
public abstract class ScopeNodeSyntax : NodeSyntax
{
    public override string ToString()
    {
        StringBuilder str = new();

        str.AppendLine($"{{");

        List<string> cLines = [];
        foreach (var i in ChildrenNodes)
            cLines.AddRange(i.ToString().Split('\n'));
        
        foreach (var i in cLines)
            str.AppendLine($"\t{i}");


        str.AppendLine($"}}");

        return str.ToString();
    }
}
public abstract class ValueNodeSyntax : NodeSyntax
{
    public virtual string ToAsmReadable() => "; not implemented ;";
}


public class IdentifierNodeSyntax(params string[] values) : NodeSyntax
{
    private List<string> _values = [.. values];
    public string[] Values => [.. _values];
    public string ValuesString => string.Join('.', _values);

    public IdentifierNodeSyntax Append(params string[] values)
    {
        _values.AddRange(values);
        return this;
    }

    public override string ToString() => ValuesString;
}

public class DebugNodeSyntax(string value) : NodeSyntax
{
    private string _value = value;
    public string Value => _value;

    public override string ToString() => _value;
}

public class TypeNodeSyntax(string value) : NodeSyntax
{

    private string _type = value;
    public string Type => _type;
    private bool _isArray = false;
    public bool IsArray => _isArray;

    public TypeNodeSyntax AsArray(bool isTrue = true)
    {
        _isArray = isTrue;
        return this;
    }

    public override string ToString() => _type + (_isArray ? "[]":"");
}

public class CompilationNodeSyntax(string fromFile) : ScopeNodeSyntax
{
    private string _filePath = fromFile;
    public string FilePath => _filePath;

    public override string ToString() => $"path: {_filePath}\n{base.ToString()}";
}


public class UsingNamespaceNodeSyntax : NodeSyntax
{
    private IdentifierNodeSyntax _identifier = null!;
    public IdentifierNodeSyntax Identifier => _identifier;

    public UsingNamespaceNodeSyntax WithIdentifier(IdentifierNodeSyntax identifier)
    {
        _identifier = identifier;
        return this;
    }

    public override string ToString() => $"using namespace {_identifier}";
}

public class MethodNodeSyntax : ScopeNodeSyntax
{
    private TypeNodeSyntax _returnType = null!;
    private IdentifierNodeSyntax _name = null!;

    public TypeNodeSyntax ReturnType => _returnType;
    public IdentifierNodeSyntax Name => _name;

    public MethodNodeSyntax WithIdentifier(IdentifierNodeSyntax name)
    {
        _name = name;
        return this;
    }
    public MethodNodeSyntax WithReturnType(TypeNodeSyntax type)
    {
        _returnType = type;
        return this;
    }

    override public string ToString()
        => $"func {_returnType} {_name}()\n{base.ToString()}";
}

public class VariableDeclarationSyntax : NodeSyntax
{
    private TypeNodeSyntax _type = null!;
    private IdentifierNodeSyntax _name = null!;
    private ValueNodeSyntax? _initialValue = null!;

    public TypeNodeSyntax Type => _type;
    public IdentifierNodeSyntax Name => _name;
    public ValueNodeSyntax? InitialValue => _initialValue;

    public VariableDeclarationSyntax WithType(TypeNodeSyntax type)
    {
        _type = type;
        return this;
    }
    public VariableDeclarationSyntax WithName(IdentifierNodeSyntax name)
    {
        _name = name;
        return this;
    }
    public VariableDeclarationSyntax WithInitialValue(ValueNodeSyntax value)
    {
        _initialValue = value;
        return this;
    }

    public override string ToString()
        => $"var {_type} {_name}" + (_initialValue != null ? $" = {_initialValue}" : "");
}

public class AsmOperationNodeSyntax(string Op) : NodeSyntax
{
    private string _operator = Op;
    private List<NodeSyntax> _args = [];

    public string Operator => _operator;
    public NodeSyntax[] Args => [.. _args];

    public AsmOperationNodeSyntax WithArgs(params NodeSyntax[] args)
    {
        _args.AddRange(args);
        return this;
    }
    public override string ToString() => $"{_operator} {string.Join(", ", _args)}";
}

public class AssiginNodeSyntax() : NodeSyntax
{
    private IdentifierNodeSyntax _destiny = null!;
    private ValueNodeSyntax _value = null!;

    public IdentifierNodeSyntax Destiny => _destiny;
    public ValueNodeSyntax Value => _value;

    public AssiginNodeSyntax WithDestiny(IdentifierNodeSyntax name)
    {
        _destiny = name;
        return this;
    }
    public AssiginNodeSyntax WithValue(ValueNodeSyntax value)
    {
        _value = value;
        return this;
    }

    public override string ToString() => $"{_destiny} = {_value}";
}

public class CallNodeSyntax() : NodeSyntax
{
    private IdentifierNodeSyntax _method = null!;
    private ArgumentsNodeSyntax _args = null!;

    public IdentifierNodeSyntax Method => _method;
    public ArgumentsNodeSyntax Arguments => _args;

    public CallNodeSyntax WithMethod(IdentifierNodeSyntax name)
    {
        _method = name;
        return this;
    }
    public CallNodeSyntax WithArgs(ArgumentsNodeSyntax args)
    {
        _args = args;
        return this;
    }

    public override string ToString() => $"{_method.ValuesString}({_args})";
}

public class ArgumentsNodeSyntax() : NodeSyntax
{
    private List<NodeSyntax> _values = [];
    public NodeSyntax[] Values => [.. _values];

    public ArgumentsNodeSyntax Append(params NodeSyntax[] args)
    {
        _values.AddRange(args);
        return this;
    }

    public override string ToString() => string.Join(", ", _values);
}

public class ReturnNodeSyntax() : NodeSyntax
{
    public override string ToString() => "return";
}

public class EOFNodeSyntax : NodeSyntax
{

}


// Value nodes
public class NumericValueNodeSyntax(string value) : ValueNodeSyntax
{
    private string _value = value;
    public string Value => _value;
    public long DecimalValue => Convert.ToInt64(_value, 16);

    public override string ToAsmReadable() => "0x" + _value.ToUpper();
    public override string ToString() => "0x" + _value.ToUpper();
}
public class StringLiteralNodeSyntax(string value) : ValueNodeSyntax
{
    private string _value = value;
    public string Value => _value;

    public override string ToAsmReadable()
    {
        var str = "\"";

        for (var i = 0; i < _value.Length; i++)
        {
            char c = _value[i];

            if (c == '\\') // verify scaped char
            {
                str += "\", ";

                str += _value[++i] switch
                {
                    'n' => (int)'\n',
                    'r' => (int)'\r',
                    '\\' => (int)'\\',

                    _ => throw new Exception("Error! Undefined scape character!"),
                };
                if (i != _value.Length-1) str += ", \"";
            }
            else
            {
                str += c;
                if (i == _value.Length-1) str += '"';
            }
        }

        return str + ", 0";
    }
    public override string ToString() => $"\"{_value}\"";
}

// Flag nodes
public class FlagStackEndNode() : NodeSyntax {}
