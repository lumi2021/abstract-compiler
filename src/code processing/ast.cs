using System.Collections;
using Compiler.CodeProcessing.CompilationStructuring;
using Newtonsoft.Json;

namespace Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;

public abstract class StatementNode {}

public class ScriptNode : ScopeNode
{
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
}

public class NamespaceNode : StatementNode
{
    public Identifier name = new();
    public ScopeNode namespaceScope = null!;
}

public class ScopeNode : StatementNode, IEnumerable<StatementNode>
{
    public List<StatementNode> body = [];

    public override string ToString() => $"{{\n\t{string.Join("\n\t", body)}\n}}";

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<StatementNode> GetEnumerator() => body.GetEnumerator();
}

public class AssemblyScopeNode : StatementNode
{
    public List<AssemblyExpressionNode> body = [];
}

public class MethodDeclarationNode : StatementNode
{
    public TypeNode returnType = null!;
    public Identifier name = new();
    public ParametersListNode parameters = null!;
    public ScopeNode methodScope = null!;
}

public class ParametersListNode : StatementNode, IEnumerable<ParameterNode>
{
    public List<ParameterNode> parameters = [];

    public override string ToString() => string.Join(", ", parameters);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<ParameterNode> GetEnumerator() => parameters.GetEnumerator();
}
public class ParameterNode : StatementNode
{
    public TypeNode type = null!;
    public string identifier = "";

    public override string ToString() => $"{type} {identifier}";    
}

public class VariableDeclarationNode : StatementNode
{
    public bool isConstant = false;
    public TypeNode type = null!;
    public Identifier identifier = new();
    public ExpressionNode? value = null;

    public override string ToString() => isConstant? "const" : "let" + $" {type} {identifier}"
    + (value != null ? $" = {value}" : "");
}

public class LocalDeclarationNode : StatementNode
{
    public bool isConstant = false;
    public TypeNode type = null!;
    public LocalRef reference = new();
    public ExpressionNode? value = null;

    public override string ToString() => isConstant? "const" : "let" + $"{type} {reference}";
}

public class ReturnStatementNode : StatementNode
{
    public ExpressionNode? value = null;

    public override string ToString() => $"return {value}";
}


public abstract class ExpressionNode : StatementNode
{
    public bool processed = false;
    public override string ToString() => $"{{ kind: {GetType().Name} }}";
}

public class AssiginmentExpressionNode : ExpressionNode
{

    public ExpressionNode assigne = null!;
    public ExpressionNode value = null!;

    public override string ToString() => $"{assigne} = {value}";
}


public class BinaryExpressionNode : ExpressionNode
{

    public ExpressionNode leftStatement = null!;
    public ExpressionNode rightStatement = null!;

    public string expOperator = "";

    public override string ToString()
    {
        var str = "";

        if (leftStatement is BinaryExpressionNode @leftBin)
        {
        
            if (expOperator != "+" && expOperator != "-" &&
            (@leftBin.expOperator == "+" || @leftBin.expOperator == "-"))
                str += $"({leftStatement})";
            else
                str += $"{leftStatement}";
        
        }
        else
            str += $"{leftStatement}";

        str += $" {expOperator} ";

        if (rightStatement is BinaryExpressionNode @rightBin)
        {

            if (expOperator != "+" && expOperator != "-" &&
            (@rightBin.expOperator == "+" || @rightBin.expOperator == "-"))
                str += $"({rightStatement})";
            else
                str += $"{rightStatement}";

        }
        else
            str += $"{rightStatement}";

        return str;
    }

}
public class UnaryExpressionNode : ExpressionNode
{
    public ExpressionNode expression = null!;

    public string expOperator = "";

    public override string ToString()
    {
        var str = $"{expOperator}";

        if (expression is BinaryExpressionNode || expression is UnaryExpressionNode)
            str += $"({expression})";
        else str += expression.ToString();

        return str;
    }
}


public class IdentifierNode(TypeItem? type = null, int? local = null) : ExpressionNode
{

    public bool isLocal = local != null; 

    public Identifier symbol = new();
    public LocalRef localRef = (local == null) ? new() : new(type!, local.Value);

    public override string ToString() => isLocal ? $"{localRef}" : $"{symbol}";

    public static bool operator ==(IdentifierNode left, IdentifierNode right) =>  left.Equals(right);
    public static bool operator !=(IdentifierNode left, IdentifierNode right) => !left.Equals(right);

    public override bool Equals(object? obj)
    {
        if (obj is IdentifierNode inode)
        {
            if (isLocal == inode.isLocal)
            {
                if (isLocal)
                    return localRef == inode.localRef;
                else
                    return symbol == inode.symbol;
            }
        }

        return false;
    }
    public override int GetHashCode() => base.GetHashCode();

}

public class MethodCallNode : ExpressionNode
{

    public Identifier target = new();
    public List<ExpressionNode> parameters = [];

    public override string ToString() => $"{target}({string.Join(", ", parameters)})";

}

public class NumericLiteralNode : ExpressionNode
{

    public double value = 0.0;

    public override string ToString() => $"{value}";
}

public class StringLiteralNode : ExpressionNode
{

    public string value = "";

    public override string ToString() => $"\"{value}\"";

}

public class NullLiteralNode : ExpressionNode
{
    public override string ToString() => "null";
}

public class AssemblyExpressionNode : ExpressionNode
{
    public AssemblyInstruction instruction = AssemblyInstruction.Undefined;
    public List<string> arguments = [];

    public override string ToString() => $"{instruction.ToString().ToUpper()} {string.Join(", ", arguments)}";
}


public abstract class TypeNode : ExpressionNode
{
    public bool isArray = false;
}
public class PrimitiveTypeNode : TypeNode
{
    public PrimitiveType value = PrimitiveType.Void;

    override public string ToString() => $"{value}";
}
public class ComplexTypeNode : TypeNode
{
    //string identifier = "";
}


public struct Identifier(TypeItem refersToType, params string[] values)
{
    public readonly List<string> values = [..values];
    public readonly bool isGlobal = false;

    public readonly TypeItem refersToType = refersToType;
    public CompStruct refersTo;

    public override string ToString() => string.Join('.', values);

    public static bool operator ==(Identifier left, Identifier right) =>  left.Equals(right);
    public static bool operator !=(Identifier left, Identifier right) => !left.Equals(right);

    public override bool Equals(object? obj)
    {
        if (obj is Identifier id)
            return Enumerable.SequenceEqual(values, id.values);
        else return false;
    }
    public override int GetHashCode() => base.GetHashCode();
}
public readonly struct LocalRef(TypeItem refersToType, int idx)
{
    public readonly int index = idx;

    public readonly TypeItem refersToType = refersToType;

    override public string ToString() => index >= 0 ? $"local.{index}" : $"arg.{Math.Abs(index)-1}";

    public static bool operator ==(LocalRef left, LocalRef right) =>  left.Equals(right);
    public static bool operator !=(LocalRef left, LocalRef right) => !left.Equals(right);

    public override bool Equals(object? obj)
    {
        if (obj is LocalRef lref)
            return index == lref.index;
        else return false;
    }
    public override int GetHashCode() => base.GetHashCode();
}

/* #################################### */
/* #### PRIMITIVE TYPES ENUMERATOR #### */
public enum PrimitiveType : byte
{
    Void = 0,

    // signed integers
    Integer_8,
    Integer_16,
    Integer_32,
    Integer_64,
    Integer_128,

    // unsigned integers
    UnsignedInteger_8,
    UnsignedInteger_16,
    UnsignedInteger_32,
    UnsignedInteger_64,
    UnsignedInteger_128,

    // floating point numbers
    Floating_32,
    Floating_64,
    SinglePrecisionFloat = Floating_32,
    DoublePrecisionFloat = Floating_64,

    // boolean
    Boolean,

    // text
    Character,
    String,

}

/* #### INTERMEDIATE ASSEMBLY INSTRUCTIONS #### */
public enum AssemblyInstruction : ushort
{
    Undefined = 0,

    Mov,
    Push,

    Add,
    Sub,
    Mul,
    Div,
}
