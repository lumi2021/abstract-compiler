# roadmap

The steps of the entire proccess to finish the compiler

### external
```
[ ] console program help
[ ] more compiling options
[ ] more debugging options
[ ] plug-ins
```

### lexing
```
[x] tokenization
[x] line breaks handling
```

### parsing
```
[ ] syntax parsing
╠══ [ ] Statements
║... ╠══ [x] namespace declaration
║... ╠══ [ ] neasted namespaces
║... ╠══ [x] method declaration
║... ╠══ [ ] struct declatation
║... ╠══ [x] scope declaration
║... ╠══ [x] local variable declatation 
║... ╠══ [ ] global variable declatation
║... ╠══ [x] inline assembly line
║... ╠══ [x] inline assembly block
║... ╠══ [x] conditional `if`
║... ╠══ [x] conditional `elif`
║... ╠══ [x] conditional `else`
║... ╠══ [ ] looping `while`
║... ╠══ [ ] looping `for`
║... ╠══ [ ] looping `do .. while`
║... ╠══ [x] converting `as` operator
║... ╚══ [ ] breaking loop
╠══ [ ] Expressions:
║... ╠══ [x] literal numbers
║... ╠══ [x] literal floatings
║... ╠══ [x] literal strings
║... ╠══ [x] literal booleans
║... ╠══ [ ] literal null
║... ╠══ [ ] literal arrays
║... ╠══ [x] primitive types
║... ╠══ [ ] collections and arrays
║... ╠══ [\] references and pointers
║... ╠══ [x] unary operators ( `-`, `+` )
║... ╠══ [x] binary operators ( `+`, `-`, `*`, `/` , `%`, `**` )
║... ╠══ [ ] binary boolean operators ( `||`, `&&`, `and`, `or` , `xor`, `nor` )
║... ╠══ [ ] boolean comparation operators ( `>=`, `<=`, `==`, `!=`, `<`, `>` )
║... ╠══ [ ] unary boolean operators ( `!`, `not` )
║... ╠══ [ ] binary bitwise operators ( `|`, `&`, `^`, `band`, `bor`, `bxor`, `bnor`, `<<`, `>>` )
║... ╠══ [ ] unary bitwise operators ( `bNot` )
║... ╠══ [ ] assiginment binary operators ( `+=`, `-=`, `*=`, `/=`, `%=`, `**=` )
║... ╠══ [ ] type modifiers ( `*`, `&`, `[]` )
║... ╠══ [ ] string concatenation and operations
║... ╚══ [ ] comptime string concatenation
╠══ [x] single-line comments (# ...)
╚══ [x] multi-line comments (### ... ###)
```
```
[ ] evaluation
╠══ [x] variable checking
╠══ [x] stack size counter
╠══ [x] scoped stack size counter
╠══ [ ] conditional and loops scoping
╠══ [ ] generic code scoping
╠══ [x] typing conversion
╠══ [x] ast to abstracted instructions
╠══ [x] assembly statements
╠══ [ ] optimizations
║... ╠══ [\] operations in comptime
║... ╠══ [ ] dead code reducement
║... ╚══ [ ] conditionals with constants reducement
```

### compiling
```
[ ] compiling
╠══ [x] abstract standard(std) lib
╚══ [ ] all instructions being handled well
```
```
[ ] default compilation targets
╠══ [ ] 16 bit assembly
╠══ [\] x86 assembly
╠══ [ ] x64 assembly
╠══ [ ] Windows 10+
╠══ [ ] Linux
╠══ [ ] MacOS
╠══ [\] WebAssembly
╚══ [ ] .NET assembly
```
```
[ ] plugins to another targets
```

### debugging & dev-assistence
```
[X] syntax DEBUGGING
[X] evaluation DEBUGGING
[\] assembling DEBUGGING
```
