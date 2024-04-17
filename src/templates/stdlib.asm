global _main
; methods from c std lib
extern _printf

; wrappers
Std.Print:     jmp _printf
