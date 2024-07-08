const fs = require('fs');
const wasmBuffer = fs.readFileSync('./test-code/bin/main.wasm');

let memory;
let memoryView;

let heapPointer = 0;

// imports here
var readline = require('readline-sync');

// std lib
const std_lib = {
    "Std.Console": {
        "Write?i64": (value) => process.stdout.write(String(Number(value))),
        "Write?f64": (value) => process.stdout.write(String(Number(value))),
        "Write?str": (value) => {
            let i32value = Number(value);
            let length = memoryView.getInt32(i32value, true);
            const stringBytes = new Uint8Array(memory.buffer, i32value + 4, length);
            process.stdout.write(new TextDecoder('utf-8').decode(stringBytes));
        },

        "Log?i64": (value) => process.stdout.write(Number(value) + "\n"),
        "Log?f64": (value) => process.stdout.write(Number(value) + "\n"),
        "Log?str": (value) => {
            let i32value = Number(value);
            let length = memoryView.getInt32(i32value, true);
            const stringBytes = new Uint8Array(memory.buffer, i32value + 4, length);
            process.stdout.write(new TextDecoder('utf-8').decode(stringBytes) + "\n");
        },

        "Read?": () => {
            let curPtr = heapPointer;
            
            const res = readline.question("");

            const stringBytes = new TextEncoder().encode(res);
            let length = stringBytes.length;

            memoryView.setUint32(curPtr, length, true);

            let view = new Uint8Array(memory.buffer);
            view.set(stringBytes, curPtr + 4);

            heapPointer += 4 + length;

            return BigInt(curPtr);
        }
    },

    "Std.Type.Casting": {
        "Cast_i32?str" : (value) => {
            let i32value = Number(value);
            let length = memoryView.getInt32(i32value, true);
            const stringBytes = new Uint8Array(memory.buffer, i32value + 4, length);
            const string = new TextDecoder('utf-8').decode(stringBytes);

            return Number(string);
        }
    },

    "Std.Type.String": {
        "Equals?str_str" : (ptr1, ptr2) => {

            var numPtr1 = Number(ptr1);
            var numPtr2 = Number(ptr2);

            if (numPtr1 == numPtr2) return true;

            let length1 = memoryView.getUint32(numPtr1, true);
            let length2 = memoryView.getUint32(numPtr2, true);

            if (length1 != length2) return false;
            
            let view = new Uint8Array(memory.buffer);

            for (let i = 0; i < length1; i++)
                if (view[numPtr1 + 4 + i] != view[numPtr2 + 4 + i]) return false;

            return true;
        }
    },

    "Std.Memory": {
        "GenArray?i32" : (len) => {

            let ptr = heapPointer;

            console.log("generating array from ptr " + ptr + " with len " + len);
            heapPointer += 4 + len;

            return BigInt(ptr);

        },
        "LoadString?str" : (source) => {

            let curPtr = heapPointer;
            let i32ptr = Number(source);

            let length = memoryView.getUint32(i32ptr, true);
            let view = new Uint8Array(memory.buffer);
            view.set(view.subarray(i32ptr, i32ptr + 4 + length), curPtr);

            heapPointer += 4 + length;

            return BigInt(curPtr);

        },
    }
};


WebAssembly.instantiate(wasmBuffer, std_lib).then((obj) => {

    memory = obj.instance.exports.mem;
    memoryView = new DataView(memory.buffer);

    heapPointer = memoryView.getInt32(0, true);

    console.log("Executing main code:");
    obj.instance.exports["MyProgram.Main?"]();

    console.log("\nExecuting tests:");
    // Tests here

    console.log("Tests finished!");

});
