﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WebAssemblyInfo
{
    class WasmReader
    {
        BinaryReader reader;

        public WasmReader(string path)
        {
            if (Program.Verbose)
                Console.WriteLine($"Reading wasm file: {path}");

            var stream = File.Open(path, FileMode.Open);
            reader = new BinaryReader(stream);
        }

        public void Parse()
        {
            ReadModule();
        }

        void ReadModule()
        {
            byte[] magicWasm = { 0x0, 0x61, 0x73, 0x6d };
            var magicBytes = reader.ReadBytes(4);

            for (int i = 0; i < magicWasm.Length; i++)
            {
                if (magicWasm[i] != magicBytes[i])
                    throw new FileLoadException("not wasm file, module magic is wrong");
            }

            var version = reader.ReadUInt32();
            if (Program.Verbose)
                Console.WriteLine($"WebAssembly binary format version: {version}");

            while (reader.BaseStream.Position < reader.BaseStream.Length)
                ReadSection();
        }

        enum SectionId
        {
            Custom = 0,
            Type,
            Import,
            Function,
            Table,
            Memory,
            Global,
            Export,
            Start,
            Element,
            Code,
            Data,
            DataCount
        }

        void ReadSection()
        {
            SectionId id = (SectionId)reader.ReadByte();
            UInt32 size = ReadU32();

            if (Program.Verbose)
                Console.Write($"Section: {id,9} size: {size,12}");

            var begin = reader.BaseStream.Position;

            switch (id)
            {
                case SectionId.Custom:
                    ReadCustomSection();
                    break;
                case SectionId.Type:
                    ReadTypeSection();
                    break;
                case SectionId.Function:
                    ReadFunctionSection();
                    break;
                case SectionId.Export:
                    ReadExportSection();
                    break;
                case SectionId.Import:
                    ReadImportSection();
                    break;
                case SectionId.Code:
                    ReadCodeSection();
                    break;
                default:
                    break;
            }

            if (Program.Verbose)
                Console.WriteLine();

            reader.BaseStream.Seek(begin + size, SeekOrigin.Begin);
        }

        struct LocalsBlock
        {
            public UInt32 Count;
            public ValueType Type;
        }

        struct Code
        {
            public LocalsBlock[] Locals;
        }

        Code[]? funcsCode;
        void ReadCodeSection()
        {
            UInt32 count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            funcsCode = new Code[count];

            for (int i = 0; i < count; i++)
            {
                var size = ReadU32();
                var pos = reader.BaseStream.Position;

                if (Program.Verbose2)
                    Console.WriteLine($"  code: {size} bytes");

                var vecSize = ReadU32();
                funcsCode[i].Locals = new LocalsBlock[vecSize];

                if (Program.Verbose2)
                    Console.WriteLine($"  locals blocks count {vecSize}");

                for (var j = 0; j < vecSize; j++)
                {
                    funcsCode[i].Locals[j].Count = ReadU32();
                    ReadValueType(ref funcsCode[i].Locals[j].Type);

                    if (Program.Verbose2)
                        Console.WriteLine($"    locals count: {funcsCode[i].Locals[j].Count} type: {funcsCode[i].Locals[j].Type}");
                }

                // read expr
                List<Instruction> instructions = new();
                do
                {
                    var b = reader.ReadByte();
                    if (b == 0xb)
                        break;

                    Console.WriteLine($"    opcode: {(Opcode)b}");
                    instructions.Add(ReadInstruction(b));

                    while (reader.ReadByte() != 0xb) ;

                    break;

                } while (true);

                reader.BaseStream.Seek(pos + size, SeekOrigin.Begin);
            }
        }

        struct Instruction
        {
            public byte Opcode;
        }

        enum Opcode : byte
        {
            // control
            Unreachable = 0x00,
            Nop = 0x01,
            Block = 0x02,
            Loop = 0x03,
            If = 0x04,
            Else = 0x05,
            Br = 0x0c,
            Br_If = 0x0d,
            Br_Table = 0x0e,
            Return = 0x0f,
            Call = 0x10,
            Call_Indirect = 0x11,
            // reference
            Ref_Null = 0xd0,
            Ref_Is_Null = 0xd1,
            Ref_Func = 0xd2,
            // parametric
            Drop = 0x1a,
            Select = 0x1b,
            Select_Vec = 0x1c,
            // variable
            Local_Get = 0x20,
            Local_Set = 0x21,
            Local_Tee = 0x22,
            Global_Get = 0x23,
            Global_Set = 0x24,
            // table
            Table_Get = 0x25,
            Table_Set = 0x26,
            // memory
            I32_Load = 0x28,
            I64_Load = 0x29,
            F32_Load = 0x2a,
            F64_Load = 0x2b,
            I32_Load8_S = 0x2c,
            I32_Load8_U = 0x2d,
            I32_Load16_S = 0x2e,
            I32_Load16_U = 0x2f,
            I64_Load8_S = 0x30,
            I64_Load8_U = 0x31,
            I64_Load16_S = 0x32,
            I64_Load16_U = 0x33,
            I64_Load32_S = 0x34,
            I64_Load32_U = 0x35,
            I32_Store = 0x36,
            I64_Store = 0x37,
            F32_Store = 0x38,
            F64_Store = 0x39,
            I32_Store8 = 0x3a,
            I32_Store16 = 0x3b,
            I64_Store8 = 0x3c,
            I64_Store16 = 0x3d,
            I64_Store32 = 0x3e,
            Memory_Size = 0x3f,
            Memory_Grow = 0x40,
            // numeric
            I32_Const = 0x41,
            I64_Const = 0x42,
            F32_Const = 0x43,
            F64_Const = 0x44,
            I32_Eqz = 0x45,
            I32_Eq = 0x46,
            I32_Ne = 0x47,
            I32_Lt_S = 0x48,
            I32_Lt_U = 0x49,
            I32_Gt_S = 0x4a,
            I32_Gt_U = 0x4b,
            I32_Le_S = 0x4c,
            I32_Le_U = 0x4d,
            I32_Ge_S = 0x4e,
            I32_Ge_U = 0x4f,
            I64_Eqz = 0x50,
            I64_Eq = 0x51,
            I64_Ne = 0x52,
            I64_Lt_S = 0x53,
            I64_Lt_U = 0x54,
            I64_Gt_S = 0x55,
            I64_Gt_U = 0x56,
            I64_Le_S = 0x57,
            I64_Le_U = 0x58,
            I64_Ge_S = 0x59,
            I64_Ge_U = 0x5a,
            F32_Eq = 0x5b,
            F32_Ne = 0x5c,
            F32_Lt = 0x5d,
            F32_Gt = 0x5e,
            F32_Le = 0x5f,
            F32_Ge = 0x60,
            F64_Eq = 0x61,
            F64_Ne = 0x62,
            F64_Lt = 0x63,
            F64_Gt = 0x64,
            F64_Le = 0x65,
            F64_Ge = 0x66,
            I32_Clz = 0x67,
            I32_Ctz = 0x68,
            I32_Popcnt = 0x69,
            I32_Add = 0x6a,
            I32_Sub = 0x6b,
            I32_Mul = 0x6c,
            I32_Div_S = 0x6d,
            I32_Div_U = 0x6e,
            I32_Rem_S = 0x6f,
            I32_Rem_U = 0x70,
            I32_And = 0x71,
            I32_Or = 0x72,
            I32_Xor = 0x73,
            I32_Shl = 0x74,
            I32_Shr_S = 0x75,
            I32_Shr_U = 0x76,
            I32_Rotl = 0x77,
            I32_Rotr = 0x78,
            I64_Clz = 0x79,
            I64_Ctz = 0x7a,
            I64_Popcnt = 0x7b,
            I64_Add = 0x7c,
            I64_Sub = 0x7d,
            I64_Mul = 0x7e,
            I64_Div_S = 0x7f,
            I64_Div_U = 0x80,
            I64_Rem_S = 0x81,
            I64_Rem_U = 0x82,
            I64_And = 0x83,
            I64_Or = 0x84,
            I64_Xor = 0x85,
            I64_Shl = 0x86,
            I64_Shr_S = 0x87,
            I64_Shr_U = 0x88,
            I64_Rotl = 0x89,
            I64_Rotr = 0x8a,
            F32_Abs = 0x8b,
            F32_Neg = 0x8c,
            F32_Ceil = 0x8d,
            F32_Floor = 0x8e,
            F32_Trunc = 0x8f,
            F32_Nearest = 0x90,
            F32_Sqrt = 0x91,
            F32_Add = 0x92,
            F32_Sub = 0x93,
            F32_Mul = 0x94,
            F32_Div = 0x95,
            F32_Min = 0x96,
            F32_Max = 0x97,
            F32_Copysign = 0x98,
            F64_Abs = 0x99,
            F64_Neg = 0x9a,
            F64_Ceil = 0x9b,
            F64_Floor = 0x9c,
            F64_Trunc = 0x9d,
            F64_Nearest = 0x9e,
            F64_Sqrt = 0x9f,
            F64_Add = 0xa0,
            F64_Sub = 0xa1,
            F64_Mul = 0xa2,
            F64_Div = 0xa3,
            F64_Min = 0xa4,
            F64_Max = 0xa5,
            F64_Copysign = 0xa6,
            I32_Wrap_I64 = 0xa7,
            I32_Trunc_F32_S = 0xa8,
            I32_Trunc_F32_U = 0xa9,
            I32_Trunc_F64_S = 0xaa,
            I32_Trunc_F64_U = 0xab,
            I64_Extend_I32_S = 0xac,
            I64_Extend_I32_U = 0xad,
            I64_Trunc_F32_S = 0xae,
            I64_Trunc_F32_U = 0xaf,
            I64_Trunc_F64_S = 0xb0,
            I64_Trunc_F64_U = 0xb1,
            F32_Convert_I32_S = 0xb2,
            F32_Convert_I32_U = 0xb3,
            F32_Convert_I64_S = 0xb4,
            F32_Convert_I64_U = 0xb5,
            F32_Demote_F64 = 0xb6,
            F64_Convert_I32_S = 0xb7,
            F64_Convert_I32_U = 0xb8,
            F64_Convert_I64_S = 0xb9,
            F64_Convert_I64_U = 0xba,
            F64_Promote_F32 = 0xbb,
            I32_Reinterpret_F32 = 0xbc,
            I64_Reinterpret_F64 = 0xbd,
            F32_Reinterpret_I32 = 0xbe,
            F64_Reinterpret_I64 = 0xbf,
            I32_Extend8_S = 0xc0,
            I32_Extend16_S = 0xc1,
            I64_Extend8_S = 0xc2,
            I64_Extend16_S = 0xc3,
            I64_Extend32_S = 0xc4,
            // special
            Prefix = 0xfc,
        }

        Instruction ReadInstruction(byte opcode)
        {
            Instruction instruction;
            instruction.Opcode = opcode;

            return instruction;
        }

        void ReadCustomSection()
        {
            if (Program.Verbose)
                Console.Write($" name: {ReadString()}");
        }

        enum ExportDesc : Byte
        {
            FuncIdx = 0,
            TableIdx,
            MemIdx,
            GlobalIdx
        }

        struct Export
        {
            public string Name;
            public UInt32 Idx;
            public ExportDesc Desc;

            public override string ToString()
            {
                return $"(export \"{Name}\" ({Desc} {Idx}))";
            }
        }

        Export[]? exports;

        void ReadExportSection()
        {
            UInt32 count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            exports = new Export[count];

            for (int i = 0; i < count; i++)
            {
                exports[i].Name = ReadString();
                exports[i].Desc = (ExportDesc)reader.ReadByte();
                exports[i].Idx = ReadU32();

                if (Program.Verbose2)
                    Console.WriteLine($"  {exports[i]}");
            }
        }

        enum ImportDesc : Byte
        {
            TypeIdx = 0,
            TableIdx,
            MemIdx,
            GlobalIdx
        }

        struct Import
        {
            public string Module;
            public string Name;
            public UInt32 Idx;
            public ImportDesc Desc;

            public override string ToString()
            {
                return $"(import \"{Module}\" \"{Name}\" ({Desc} {Idx}))";
            }
        }

        Import[]? imports;

        void ReadImportSection()
        {
            UInt32 count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            imports = new Import[count];

            for (int i = 0; i < count; i++)
            {
                imports[i].Module = ReadString();
                imports[i].Name = ReadString();
                imports[i].Desc = (ImportDesc)reader.ReadByte();
                imports[i].Idx = ReadU32();

                if (Program.Verbose2)
                    Console.WriteLine($"  {imports[i]}");
            }
        }

        string ReadString()
        {
            return Encoding.UTF8.GetString(reader.ReadBytes((int)ReadU32()));
        }

        struct Function
        {
            public UInt32 TypeIdx;
        }

        Function[]? functions;

        public enum NumberType : Byte
        {
            i32 = 0x7f,
            i64 = 0x7e,
            f32 = 0x7d,
            f64 = 0x7c
        }

        public enum ReferenceType : Byte
        {
            FuncRef = 0x70,
            ExternRef = 0x6f,
        }

        [StructLayout(LayoutKind.Explicit)]
        struct ValueType
        {
            [FieldOffset(0)]
            public byte value;
            [FieldOffset(0)]
            public NumberType Number;
            [FieldOffset(0)]
            public ReferenceType Reference;

            [FieldOffset(1)]
            public bool IsRefenceType;

            public override string ToString()
            {
                return IsRefenceType ? Reference.ToString() : Number.ToString();
            }
        }

        struct ResultType
        {
            public ValueType[] Types;

            public override string ToString()
            {
                StringBuilder sb = new();

                for (var i = 0; i < Types.Length; i++)
                {
                    if (i > 0)
                        sb.Append(' ');

                    sb.Append(Types[i].ToString());
                }

                return sb.ToString();
            }
        }

        struct FunctionType
        {
            public ResultType Parameters;
            public ResultType Results;

            public override string ToString()
            {
                var results = Results.Types.Length == 0 ? "" : $" (result {Results.ToString()})";
                return $"(func (param {Parameters}){results})";
            }
        }

        FunctionType[]? functionTypes;

        void ReadTypeSection()
        {
            UInt32 count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            functionTypes = new FunctionType[count];
            for (int i = 0; i < count; i++)
            {
                var b = reader.ReadByte();
                if (b != 0x60)
                    throw new FileLoadException("Expected 0x60 for function type");

                ReadResultTypes(ref functionTypes[i].Parameters);
                ReadResultTypes(ref functionTypes[i].Results);

                if (Program.Verbose2)
                    Console.WriteLine($"  Function type[{i}]: {functionTypes[i]}");
            }
        }

        void ReadResultTypes(ref ResultType type)
        {
            UInt32 count = ReadU32();

            type.Types = new ValueType[count];

            for (int i = 0; i < count; i++)
            {
                ReadValueType(ref type.Types[i]);
            }
        }

        void ReadValueType(ref ValueType vt)
        {
            var b = reader.ReadByte();
            vt.IsRefenceType = b <= 0x70;
            vt.value = b;
        }

        void ReadFunctionSection()
        {
            UInt32 count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            functions = new Function[count];

            for (int i = 0; i < count; i++)
            {
                functions[i].TypeIdx = ReadU32();
            }
        }

        UInt32 ReadU32()
        {
            UInt32 value = 0;
            var offset = 0;
            do
            {
                var b = reader.ReadByte();
                value |= (UInt32)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    break;

                offset += 7;
            } while (true);

            return value;
        }
    }
}
