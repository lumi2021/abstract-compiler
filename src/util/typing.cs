using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;

namespace Compiler.Util.Compilation;

public static class Typing
{
    public static string TypeAsILString(PrimitiveTypeList t)
    {
        return t switch
        {
            PrimitiveTypeList.Void => "void",
            PrimitiveTypeList.Integer_8 => "i8",
            PrimitiveTypeList.Integer_16 => "i16",
            PrimitiveTypeList.Integer_32 => "i32",
            PrimitiveTypeList.Integer_64 => "i64",
            PrimitiveTypeList.Integer_128 => "i128",
            PrimitiveTypeList.UnsignedInteger_8 => "u8",
            PrimitiveTypeList.UnsignedInteger_16 => "u16",
            PrimitiveTypeList.UnsignedInteger_32 => "u32",
            PrimitiveTypeList.UnsignedInteger_64 => "u64",
            PrimitiveTypeList.UnsignedInteger_128 => "u128",
            PrimitiveTypeList.Floating_32 => "f32",
            PrimitiveTypeList.Floating_64 => "f64",
            PrimitiveTypeList.Boolean => "bool",
            PrimitiveTypeList.Character => "char",
            PrimitiveTypeList.String => "str",

            _ => throw new NotImplementedException($"{t}")
        };
    }

    public static PrimitiveTypeList ILStringAsType(string s)
    {
        return s switch
        {
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
            "ptr" => PrimitiveTypeList.Pointer,

            _ => throw new NotImplementedException(s)
        };
    }

    // primitives
    public static int SizeOf(PrimitiveTypeList type)
    {
        return type switch
        {
            PrimitiveTypeList.Boolean or
            PrimitiveTypeList.UnsignedInteger_8 or
            PrimitiveTypeList.Integer_8 => 1,

            PrimitiveTypeList.UnsignedInteger_16 or
            PrimitiveTypeList.Integer_16 => 2,

            PrimitiveTypeList.Character or
            PrimitiveTypeList.UnsignedInteger_32 or
            PrimitiveTypeList.Integer_32 or
            PrimitiveTypeList.Floating_32 => 4,

            PrimitiveTypeList.UnsignedInteger_64 or
            PrimitiveTypeList.Integer_64 or
            PrimitiveTypeList.Floating_64 => 8,

            PrimitiveTypeList.UnsignedInteger_128 or
            PrimitiveTypeList.Integer_128 => 0, // 16
            
            PrimitiveTypeList.Void or
            PrimitiveTypeList.Pointer or
            PrimitiveTypeList.String => 0,

            _ => throw new NotImplementedException()
        };
    }

    public static ExpressionNode DefaultValueOf(PrimitiveTypeList type)
    {
        return type switch
        {
            PrimitiveTypeList.Void                  => throw new NotImplementedException(),
            PrimitiveTypeList.Character             => throw new NotImplementedException(),
            PrimitiveTypeList.Boolean               => throw new NotImplementedException(),

            PrimitiveTypeList.Integer_8             or
            PrimitiveTypeList.UnsignedInteger_8     or
            PrimitiveTypeList.Integer_16            or
            PrimitiveTypeList.UnsignedInteger_16    or
            PrimitiveTypeList.Integer_32            or
            PrimitiveTypeList.UnsignedInteger_32    or
            PrimitiveTypeList.Floating_32           or
            PrimitiveTypeList.Integer_64            or
            PrimitiveTypeList.UnsignedInteger_64    or
            PrimitiveTypeList.Floating_64           => new NumericLiteralNode() { value = 0 },

            _ => throw new NotImplementedException(type.ToString())
        };
    }

    public static long MinValueOf(PrimitiveTypeList type) => type switch
    {
        PrimitiveTypeList.Integer_8    => -(long)Math.Pow(2, 7),
        PrimitiveTypeList.Integer_16   => -(long)Math.Pow(2, 15),
        PrimitiveTypeList.Integer_32   => -(long)Math.Pow(2, 31),
        PrimitiveTypeList.Integer_64   => -(long)Math.Pow(2, 63),
        // PrimitiveTypeList.Integer_128  => -(long)Math.Pow(2, 127),

        PrimitiveTypeList.UnsignedInteger_8 or
        PrimitiveTypeList.UnsignedInteger_16 or
        PrimitiveTypeList.UnsignedInteger_32 or
        PrimitiveTypeList.UnsignedInteger_64 or
        // PrimitiveTypeList.UnsignedInteger_128 => 0,

        _ => 0
    };
    public static long MaxValueOf(PrimitiveTypeList type) => type switch
    {
        PrimitiveTypeList.Integer_8    => (long)Math.Pow(2, 7) -1,
        PrimitiveTypeList.Integer_16   => (long)Math.Pow(2, 15) -1,
        PrimitiveTypeList.Integer_32   => (long)Math.Pow(2, 31) -1,
        PrimitiveTypeList.Integer_64   => (long)Math.Pow(2, 63) -1,
        // PrimitiveTypeList.Integer_128  => (long)Math.Pow(2, 127) -1,

        PrimitiveTypeList.UnsignedInteger_8   => (long)Math.Pow(2, 8) -1,
        PrimitiveTypeList.UnsignedInteger_16  => (long)Math.Pow(2, 16) -1,
        PrimitiveTypeList.UnsignedInteger_32  => (long)Math.Pow(2, 32) -1,
        PrimitiveTypeList.UnsignedInteger_64  => (long)Math.Pow(2, 64) -1,
        // PrimitiveTypeList.UnsignedInteger_128 => (long)Math.Pow(2, 128),

        _ => 0
    };

    public static PrimitiveTypeKind KindOf(PrimitiveTypeList type) => type switch
    {
        PrimitiveTypeList.__Generic__Number or
        PrimitiveTypeList.Integer_8 or
        PrimitiveTypeList.Integer_16 or
        PrimitiveTypeList.Integer_32 or
        PrimitiveTypeList.Integer_64 or
        PrimitiveTypeList.Integer_128 or
        PrimitiveTypeList.UnsignedInteger_8 or
        PrimitiveTypeList.UnsignedInteger_16 or
        PrimitiveTypeList.UnsignedInteger_32 or
        PrimitiveTypeList.UnsignedInteger_64 or
        PrimitiveTypeList.UnsignedInteger_128 => PrimitiveTypeKind.IntegerNumeric,
        
        PrimitiveTypeList.__Generic__Floating or
        PrimitiveTypeList.Floating_32 or
        PrimitiveTypeList.Floating_64 => PrimitiveTypeKind.FloatingNumeric,

        PrimitiveTypeList.Boolean => PrimitiveTypeKind.Boolean,

        PrimitiveTypeList.Character => PrimitiveTypeKind.Character,

        PrimitiveTypeList.Void or
        PrimitiveTypeList.String or
        PrimitiveTypeList.Pointer => PrimitiveTypeKind.Pointer,

        _ => throw new NotImplementedException()
    };
}

public interface ILangType
{
    public TypeDefKind ReferingTo { get; }
    public int Size { get; }
    public string ToIlString();
}

public readonly struct PrimitiveType(PrimitiveTypeList value, TypeDefKind refTo) : ILangType{

    private readonly PrimitiveTypeList _value = value;
    public PrimitiveTypeList Value => _value;

    private readonly TypeDefKind _referingTo = refTo;

    public TypeDefKind ReferingTo => _referingTo;
    public int Size => Typing.SizeOf(_value);

    public long MinValue => Typing.MinValueOf(_value);
    public long MaxValue => Typing.MaxValueOf(_value);

    public PrimitiveTypeKind Kind => Typing.KindOf(_value);

    public string ToIlString() => Typing.TypeAsILString(_value);
    public override string ToString()
    {
        var str = "";

        if (_referingTo == TypeDefKind.Reference) str += "*";
        if (_referingTo == TypeDefKind.Pointer) str += "&";

        str += $"{_value}";

        return str;
    }

}

//public struct ComplexType : ILangType {}

public enum TypeDefKind : byte
{
    Value,
    Reference,
    Pointer
}

public enum PrimitiveTypeList : byte
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

    // memory
    Pointer,


    // generic
    __Generic__Number,
    __Generic__Floating,

}

public enum PrimitiveTypeKind : byte
{
    IntegerNumeric,
    FloatingNumeric,

    Boolean,
    Character,

    Pointer
}
