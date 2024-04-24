# Abstract Syntax Reference

This document contains the basic syntax of the Abstract language.
This document is used as an guide and chan be changed with the time.

# Program example
```
using namespace MyProgram
/*
    This is my program :)
*/

func void Start()
{
    var byte foo = 10
    var int bar = 30

    Std.Print("Hello, World!\n")
    Std.Print("Foo is %d and bar is %d.\n", foo, bar)

    foo = 255
    Std.Print("Now, foo is %d and bar is %d!\n", foo, bar)

    // assemby x86 code :O
    asm jmp MyProgram.GoodBye?
}

func void GoodBye()
{
    Std.Print("Goodbye, %s!\n", "World")

    return 0
}

```

# Primitive data types
```
### UNSIGNED INTEGERS ###
# byte                  0 <=>           255
# uint16                0 <=>        65.535
# uint(32)              0 <=> 4.294.967.295
# uint64                0 <=>         2e+64
# uint128               0 <=>        2e+128


### SIGNED INTEGERS ###
# syte               -127 <=>           127
# int16           -32.767 <=>        32.767
# int(32)  -2.147.483.647 <=> 2.147.483.647
# int64            -2e+63 <=>        -2e+63
# int128          -2e+127 <=>       -2e+127


### FLOATING POINT NUMBERS ###
# float        32b - precision x1
# double       64b - precision x2


### BOOLEANS ###
bool           true or false


### CHARACTERS AND STRINGS ###
# char    - stores an character encoded as UTF-8
# string  - immutable array of chars


### ARRAYS ###
# to define arrays, you need to add "[]" after the
# type declaration. e. g.:

# byte[], int16[], string[]


### IN PRACTICE ###
# to declare a variable, use the keyword var, the type and it identifier.
# If you need, add an initial value
var byte MyByte = 20

# constants can be also defined with the keyword const
const uint ConstantInteger = 0

```

# Logic
```
// parenthesis are optional on if statements
if x > 10 => return 0
if (x > 11) => return 1

/*
    this block will not be compiled in the final
    result due it's impossible to be true
*/
if (myByte > 255)
{
    // really long code here
}

# \/ implicit var declaration
for byte i = 0; i < 10; i++
{
    i++
}

foreach i in 10 => x++
foreach i in myArray
{
    Std.Print(i)
}

while (true) => break
while true
{
    break
}

```

