global _main
extern _printf, _fgetc, _malloc, _realloc, _free

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
    enter 17, 0
    ; *char (4)  buffer  ->  BP - 4
    ; u32   (4)  size    ->  BP - 8
    ; char  (1)  char    ->  BP - 9
    ; u32   (4)  length  ->  BP - 13

    ; test for non-null values
    test DWORD[EBP + 4], 0
    jz .err
    test DWORD[EBP + 8], 0
    jz .err
    test DWORD[EBP + 12], 0
    jz .err

    ; initialize the buffer
    mov DWORD[EBP - 8], 98
    push DWORD[EBP - 8]
    call _malloc
    add ESP, 4
    mov DWORD[EBP - 4], EAX

    test EAX, 0
    jz .err

    ; loop though the string
    .while_001:
        call _fgetc
        movzx EAX, al
        cmp EAX, 0
        jl .break_001
        mov DWORD[BP - 9], EAX

        mov EAX, DWORD[EBP - 13]
        inc EAX
        cmp EAX, DWORD[EBP - 8]
        jl .endif_001

            sal DWORD[EBP - 8], 1

            push DWORD[ebp - 8]
            push DWORD[ebp - 4]
            call _realloc
            add ESP, 64

            test EAX, 0
            jne .l_0001
                push DWORD[EBP - 4]
                call _free
                add ESP, 4
                jmp .err
            .l_0001:
            mov [EBP - 4], EAX
            
        .endif_001:

        cmp DWORD[EBP - 9], 10 ; 10 = '\n' char
        je .break_001

        mov EAX , DWORD[BP - 4] ; pointer
        mov EBX, DWORD[EBP - 13] ; index
        mov ECX, DWORD[EBP - 9] ; value
        mov DWORD[EAX + EBX * 4], ECX
        inc DWORD[EBP - 13]
    .break_001:

    ; return a new empty array if length = 0
    cmp DWORD[EBP - 13], 0
    jne .l_0002
    push 0
    mov EAX, empty_array
    jmp .ok
    .l_0002:

    ; u32   (4)  new_str  ->  BP - 17

    ; generate the new ASCII string
    push DWORD[EBP - 13]
    call Std.Memory@GenString?i32
    mov DWORD[EBP - 17], EAX

    mov EBX, DWORD[EBP - 4] ; buffer ptr
    mov ECX, 0              ; i
    .while_002:
        inc EDX
        
        mov al, BYTE[EBX + ECX]
        mov BYTE[EAX + ECX + 4], al

        inc ECX
        cmp ECX, DWORD[EBP - 13]
        jl .while_002
    
    push DWORD[EBP - 4]
    call _free
    add ESP, 4

    jmp .ok
    .err:
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
    mov EAX, DWORD[EBP + 4]
    add EAX, 5 ; length + \0
    push EAX
    call _malloc
    add ESP, 4

    test EAX, 0
    jz .err

    mov EBX, DWORD[EBP + 4]
    mov BYTE[EAX + 4 + EBX], 0 ; null char at the end

    .err:
        mov EAX, 0
    .ok:
        ret

Std.Memory@LoadString?str:

section .rodata
	format_sint         db "%i", 0
	format_uint         db "%u", 0
	format_float        db "%f", 0
	line_feed           db 10, 0
	empty_array         db 001,000,000,000, 0
