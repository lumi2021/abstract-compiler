# Abstract Syntax Reference

This document contains the basic syntax of the Abstract language.
This document is used as an guide and can be changed with the time.

# General reference
```

# Single line comment

###

    Multiline comment

###

```

# Program example
```
using Std

namespace MyProgram {
# This is my program :) #

    # functions are, as default, defined as PRIVATE
    func void Main() {

        let u8 foo = 10
        let i32 bar = 30

        Std.Console.Log("Hello, World!\n")
        Console.Log("Foo is %d and bar is %d.\n", foo, bar)

        foo = 2^8 - 1 #255
        Console.Log("Now, foo is %d and bar is %d!\n", foo, bar)

        ###
        #   ATTENTION!
        #   Compiler is throwing a warning about the line bellow
        #   as 256 will cause a overflow on the int8 (byte) type.
        #   The code is still compilable, but foo will be 0.
        ###
        foo = 2^8 #256 (overflow)

        # preprocessed inline assemby x86 code :O
        asm jmp GoodBye()
        # (obviously will not work for every target!)

    }

    # Mathematical abstraction #
    public func i32 Square(i32 number) => number ** 2
    public func i32 IsEven(i32 number) => number % 2 == 0
    public func i32 IsOdd(i32 number) => number % 2 != 0

    private func GoodBye() {

        Console.Log("Goodbye, %s!\n", "World")
        return

    }
}

```

# Primitive data types
```
### UNSIGNED INTEGERS (0 <=> 256^n-1) ###
# u8, byte  :  1 byte
# u16       :  2 bytes
# u32       :  4 bytes
# u64       :  8 bytes
# u128 temporarily removed

### SIGNED INTEGERS (irdk) ###
# i8        :  1 byte
# i16       :  2 bytes
# i32       :  4 bytes
# i64       :  8 bytes
# i128 : temporarily removed


### FLOATING POINT NUMBERS ###
# f32, float  : 4 bytes (32b) (precision x1)
# f64, double : 8 bytes (64b) (precision x2)


### BOOLEANS ###
bool : true (1) <=> false (0)


### CHARACTERS AND STRINGS ###
# char   : 4 bytes (32b) - stores an character encoded as UTF-8
# string : dynamic       - imuttable array of bytes, encoded as UTF-8

### ARRAYS ###
# to define arrays, you need to add "[]" before the
# type declaration. e. g.:
#> []i8, []int16, []string         <- arrays with undefined length
#> [8]i8, [200]int16, [0]string    <- arrays with length defined at comptime

# to define multidimensional arrays (matrices), it's
# used the comma character to separate the size of each
# dimension. e. g.:
#> [,]i8, [,,]i16, [,]string             <- matrices with undefined length
#> [2,2]i8, [8,,5]i16, [2,0]string       <- matrices with at least one of it dimensions defined at comptime


### REFERENCE AND POINTERS ###
# in Abstract language ALL data types are sent as value in method
# parameters or assiginments. To solve this problem and declarate a
# data handler that needs to be an reference handler, it's used the
# character '*' before the type. e. g.:
#> *i8, *[]i16, []*i16, *void                   <- different reference types

# the '*' character anso is used before a pointer identifier to declarate
# an assignment to the data being pointed.

# to assigin some value to the data being referenciated, it's possible
# just assigin the new data as it's done with the value data.

let i8 myByte = 10
let *i8 myByteRef
*myByteRef = &myByte   # directly setting the reference
myByteRef = myByte     # indirectly setting the reference
let myByteRef = 20
# both 'myByte' and 'myByteRef' will now refer to 20


# to manipulate the reference of a pointer or a variable, it's possible to
# use the character '&' to read the data in simple variables and read-write
# the data on other pointers.

let i8 myByte = 10
let *i8 myPtr1
let *i8 myPtr2
myPtr1 = myByte
*myPtr2 = *myPtr1
myPtr2 = 20
# 'myByte', 'myPtr1' and 'myPtr2' will now refer to 20

### watch out! ###

myPtr1 = myPtr2 # is different of
*myPtr1 = &myPtr2

# cause 'myPtr1' will now store a reference to the pointer's adress, and not the
# data being pointed!


### IN PRACTICE ###
# to declare a variable, use the keyword 'let', the type and it identifier.
# If you need, add an initial value as follows:
let i8  myByte
let i16 myShort = 20

# immutable variables (or constants) can be also defined with the keyword 'const':
const u32 ConstantInteger = 0

```

# Logic
```
### CONDITIONAL IF ###
# using the 'if' keyword, is possible to define a conditional
# point on the code. The if statement will evaluate a boolean
# expression in front of it and execute what follows the '=>'
# arrow operator.
# the basic structure of the if conditional is:
#> if [boolean value] => execute if true...
# e. g.:

let bool getCake = # insert here an dynamic value
if getCake => Std.Console.Log("The cake is a lie!")

###
    this line will not be compiled into the final
    result due it's impossible to be true
###
if myByte > 255 => Std.Console.Log("The overflow is a lie!")

### same with this line ###
if false => Std.Console.Log("The boolean of Schrödinger")

# more often, it's necessary to have more than one statement
# inside of an conditional evaluator. In this case, it's possible
# to use a code block '{ ... }' to get rid of the issue.

if roundEarth => {
    Std.Console.Log("Hmmm...")
    Std.Console.Log("I really don't know why, but...")
    Std.Console.Log("I think the earth is flat!")
}

### CONDITIONAL ELIF/ELSE ###
# in the case of needing to run some code if an if condition is
# not true and, only, if it's not true, it's used the keyword
# 'else' to run the desired statement or code block.

if someBoolean => Std.Console.Log("This is true!")
else => Std.Console.Log("This is false!")

# if instead of a simple else, the code needs to evaluate another
# condition if the first one is false, it's used the keyword 'elif'
# as the same way as in the first 'if' statement.

if someBoolean1 => Std.Console.Log("This thing is true!")
elif someBoolean2 => Std.Console.Log("These thing is true!")
else => Std.Console.Log("Everything is false!")


```

