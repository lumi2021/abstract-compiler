global _main

extern _printf, _putchar, _malloc, _realloc, _free
extern __getch

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

    push line_feed
    call _printf
    ADD ESP, 12

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
    enter 20, 0
    ; *char (4)  buffer  ->  BP - 4
    ; u32   (4)  size    ->  BP - 8
    ; char  (1)  char    ->  BP - 12
    ; u32   (4)  length  ->  BP - 16

    ; initialize variables
    mov DWORD[EBP - 8], 16 ; size = 16
    mov DWORD[EBP - 16], 0 ; length = 0

    ; allocate buffer
    push 16
    call _malloc

    mov DWORD[EBP - 4], EAX

    cmp EAX, 0
    je .err

    .while_getchar:
        call __getch
        mov BYTE[EBP - 12], AL

        cmp AL, 13
        je .end_while

        push EAX
        call _putchar
        pop EAX

        mov EAX, DWORD[EBP - 16]
        cmp EAX, DWORD[EBP - 8]
        jl .endif_001
            mov EAX, DWORD[EBP - 8]
            sal EAX, 1
            mov DWORD[EBP - 8], EAX
            
            push DWORD[EBP - 8]
            push DWORD[EBP - 4]
            call _realloc

            cmp EAX, 0
            je .err

            mov DWORD[EBP - 4], EAX
        .endif_001:
        
        mov EAX, DWORD[EBP -  4]   ; base addr
        mov EBX, DWORD[EBP - 16]   ; index
        mov DL,   BYTE[EBP - 12]   ; character
        mov BYTE[EAX + EBX], DL

        inc DWORD[EBP - 16]

        jmp .while_getchar
    .end_while:

    ; feeding line
    push line_feed
    call _printf
    add ESP, 4

    ; creating and feeding the string data structure
    push DWORD[EBP - 16]
    call Std.Memory@GenString?i32

    cmp EAX, 0
    je .err

    push EAX

    mov EDX, DWORD[EBP - 4] ; EDX is the buffer addr
    mov EBX, EAX ; EBX is the string char data base
    mov ECX, 0 ; indexer

    mov EAX, 0
    .for_start:
        cmp ECX, DWORD[EBP - 16]
        jge .for_break

        mov AL, BYTE[EDX + ECX]
        mov BYTE[EBX + 4 + ECX], AL

        inc ECX
        jmp .for_start
    .for_break:
    
    pop EAX

    jmp .ok
    .err:
        push error_str
        call _printf
        add ESP, 4

        mov eax, 0
    .ok:
        leave
        ret
    

Std.Type.String@Equals?str_str:
Std.Type.Casting@Cast_i8?str:
Std.Type.Casting@Cast_i16?str:
Std.Type.Casting@Cast_i32?str:
Std.Type.Casting@Cast_i64?str:
Std.Memory@GenArray?i32:

Std.Memory@GenString?i32:
    mov EAX, DWORD[ESP + 4]
    add EAX, 5 ; length + \0

    push EAX
    call _malloc
    add ESP, 4

    cmp EAX, 0
    je .err

    mov EBX, DWORD[ESP + 4]
    mov BYTE[EAX + 4 + EBX], 0 ; null char at the end

    jmp .ok
    .err:
        push error_str
        call _printf
        add ESP, 4
        mov EAX, 0
    .ok:
        ret

Std.Memory@LoadString?str:

___dbg___@logLineNumber?i32:
    push EAX
    push DWORD[ESP + 8]
    push line_number
    call _printf
    add ESP, 8
    pop EAX

    pop EBX
    add ESP, 4
    jmp EBX
___dbg___@logAccumulator?:
    push EBX
    push ECX
    push EDX

    push EAX
    push eax_reg_value
    call _printf
    add ESP, 4

    pop EAX
    pop EDX
    pop ECX
    pop EBX

    ret
___dbg___@logBase?:
    push EAX
    push ECX
    push EDX

    push EBX
    push ebx_reg_value
    call _printf
    add ESP, 4

    pop EBX
    pop EDX
    pop ECX
    pop EAX

    ret
___dbg___@logCounter?:
    push EAX
    push EBX
    push EDX

    push ECX
    push ecx_reg_value
    call _printf
    add ESP, 4

    pop ECX
    pop EDX
    pop EBX
    pop EAX

    ret

section .rodata
	line_number         db 9, "line:_ %i", 10, 0
	eax_reg_value       db 9, "EAX: %i", 10, 0
	ebx_reg_value       db 9, "EBX: %i", 10, 0
	ecx_reg_value       db 9, "ECX: %i", 10, 0
	error_str           db 9, "[Error!]", 10, 0

	format_sint         db "%i", 0
	format_uint         db "%u", 0
	format_float        db "%f", 0
	line_feed           db 10, 0
	empty_array         db 001,000,000,000, 0
