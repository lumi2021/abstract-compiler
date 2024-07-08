global _main
extern _printf

section .text

Std.Console@Write?i8:
Std.Console@Write?i16:
Std.Console@Write?i32:
Std.Console@Write?i64:
Std.Console@Write?u8:
Std.Console@Write?u16:
Std.Console@Write?u32:
Std.Console@Write?u64:
Std.Console@Write?f32:
Std.Console@Write?f64:
Std.Console@Write?str:

Std.Console@Log?i8:
Std.Console@Log?i16:
    
Std.Console@Log?i32:
    push DWORD[ESP + 4]
    push format_sint
    call _printf

    pop EBX
    add ESP, 4
    jmp EBX

Std.Console@Log?i64:
Std.Console@Log?u8:
Std.Console@Log?u16:
Std.Console@Log?u32:
Std.Console@Log?u64:
Std.Console@Log?f32:
Std.Console@Log?f64:

Std.Console@Log?str:
    mov EAX, DWORD[ESP + 4]
    add EAX, 4
    push EAX
    call _printf

    push line_feed
    call _printf
    ADD ESP, 8
    
    pop EBX
    add ESP, 4
    jmp EBX

Std.Console@Read?:

Std.Type.String@Equals?str_str:
Std.Type.Casting@Cast_i8?str:
Std.Type.Casting@Cast_i16?str:
Std.Type.Casting@Cast_i32?str:
Std.Type.Casting@Cast_i64?str:
Std.Memory@GenArray?i32:
Std.Memory@LoadString?str:

section .rodata
	format_sint         db "%i", 0
	format_uint         db "%u", 0
	format_float        db "%f", 0
	line_feed           db 10, 0
