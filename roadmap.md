# roadmap

The steps of the entire proccess to finish the compiler

### external
[ ] console program help
[ ] more compiling opetions
[ ] more debugging opetions

### lexing
[x] tokenization

### parsing
[ ] evaluation
╠══ [x] variable checking
╠══ [x] stack size counter
╠══ [x] scoped stack size counter
╠══ [ ] if/else labeling
╠══ [x] ast to abstracted instructions
╚══ [ ] optimizations

[ ] syntax parsing
╠══ [ ] Statements
║. . . ╠══ [x] namespace declaration
║. . . ╠══ [ ] neasted namespaces
║. . . ╠══ [x] method declaration
║. . . ╠══ [ ] struct declatation
║. . . ╠══ [x] scope declaration
║. . . ╠══ [x] local variable declatation 
║. . . ╠══ [ ] global variable declatation
║. . . ╠══ [x] inline assembly line
║. . . ╠══ [x] inline assembly block
║. . . ╠══ [ ] conditional `if`
║. . . ╠══ [ ] conditional `elif`
║. . . ╠══ [ ] conditional `else`
║. . . ╠══ [ ] looping `while`
║. . . ╠══ [ ] looping `for`
║. . . ╠══ [ ] looping `do .. while`
║. . . ╚══ [ ] breaking loop
╠══ [ ] Expressions:
║. . . ╠══ [x] literal numbers
║. . . ╠══ [ ] literal strings
║. . . ╠══ [ ] literal booleans
║. . . ╠══ [ ] literal null
║. . . ╠══ [ ] literal array
║. . . ╠══ [x] primitive types
║. . . ╠══ [ ] collections and arrays
║. . . ╠══ [ ] references and pointers
║. . . ╠══ [x] unary operators ( `-`, `+` )
║. . . ╠══ [x] binary operators ( `+`, `-`, `*`, `/` , `%`, `**` )
║. . . ╠══ [ ] binary boolean operators ( `||`, `&&`, `and`, `or` , `xor`, `nor` )
║. . . ╠══ [ ] boolean comparation operators ( `>=`, `<=`, `==`, `!=`, `<`, `>` )
║. . . ╠══ [ ] unary boolean operators ( `!`, `not` )
║. . . ╠══ [ ] binary bitwise operators ( `|`, `&`, `^`, `band`, `bor`, `bxor`, `bnor`, `<<`, `>>` )
║. . . ╠══ [ ] unary bitwise operators ( `bNot` )
║. . . ╠══ [ ] assiginment binary operators ( `+=`, `-=`, `*=`, `/=`, `%=`, `**=` )
║. . . ╚══ [ ] type modifiers ( `*`, `&`, `[]` )
╠══ [x] single-line comments (# ...)
╚══ [x] multi-line comments (### ... ###)


### compiling
[ ] compiling
╠══ [x] abstracted instructions to nasm
╠══ [x] abstract standard(std) lib
╚══ [ ] all instructions being handled well

[ ] compiled program running

### debugging & dev-assistence
[X] syntax DEBUGGING
[X] evaluation DEBUGGING
