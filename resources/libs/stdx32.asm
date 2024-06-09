global _main
extern _printf

section .text

Std.Console.Log?i32: ; Console.Log(i32)
	ENTER 0x0, 0x0

	PUSH DWORD[EBP + 8]
	PUSH _stdx32lib.integer_format_string
	call _printf
	ADD ESP, 8

	LEAVE
	POP   EBX
	ADD   ESP, 0x4
	PUSH  EBX 
	RET

section .data
	_stdx32lib:
		.integer_format_string db "%i", 10, 0
