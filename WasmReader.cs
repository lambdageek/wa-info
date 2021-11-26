﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NameMap = System.Collections.Generic.Dictionary<System.UInt32, string>;

namespace WebAssemblyInfo
{
    class WasmReader
    {
        public readonly BinaryReader Reader;
        public UInt32 Version { get; private set; }
        public string Path { get; private set; }

        public WasmReader(string path)
        {
            if (Program.Verbose)
                Console.WriteLine($"Reading wasm file: {path}");

            Path = path;
            var stream = File.Open(Path, FileMode.Open);
            Reader = new BinaryReader(stream);
        }

        public void Parse()
        {
            ReadModule();
        }

        void ReadModule()
        {
            byte[] magicWasm = { 0x0, 0x61, 0x73, 0x6d };
            var magicBytes = Reader.ReadBytes(4);

            for (int i = 0; i < magicWasm.Length; i++)
            {
                if (magicWasm[i] != magicBytes[i])
                    throw new FileLoadException("not wasm file, module magic is wrong");
            }

            Version = Reader.ReadUInt32();
            if (Program.Verbose)
                Console.WriteLine($"WebAssembly binary format version: {Version}");

            while (Reader.BaseStream.Position < Reader.BaseStream.Length)
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

        struct SectionInfo
        {
            public SectionId id;
            public UInt32 size;
        }
        List<SectionInfo> sections = new();

        void ReadSection()
        {
            var section = new SectionInfo() { id = (SectionId)Reader.ReadByte(), size = ReadU32() };
            sections.Add(section);

            if (Program.Verbose)
                Console.Write($"Reading section: {section.id,9} size: {section.size,12}");

            var begin = Reader.BaseStream.Position;

            switch (section.id)
            {
                case SectionId.Custom:
                    ReadCustomSection(section.size);
                    break;
                case SectionId.Type:
                    ReadTypeSection();
                    break;
                case SectionId.Function:
                    ReadFunctionSection();
                    break;
                case SectionId.Table:
                    ReadTableSection();
                    break;
                case SectionId.Export:
                    ReadExportSection();
                    break;
                case SectionId.Import:
                    ReadImportSection();
                    break;
                case SectionId.Element:
                    ReadElementSection();
                    break;
                case SectionId.Code:
                    if (Program.AotStats || Program.Disassemble)
                        ReadCodeSection();
                    break;
                default:
                    break;
            }

            if (Program.Verbose)
                Console.WriteLine();

            Reader.BaseStream.Seek(begin + section.size, SeekOrigin.Begin);
        }

        TableType[]? tables;
        void ReadTableSection()
        {
            UInt32 count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            tables = new TableType[count];
            for (uint i = 0; i < count; i++)
            {
                tables[i].RefType = (ReferenceType)Reader.ReadByte();
                var limitsType = Reader.ReadByte();
                tables[i].Min = ReadU32();
                tables[i].Max = limitsType == 1 ? ReadU32() : UInt32.MaxValue;

                if (Program.Verbose2)
                    Console.WriteLine($"  table: {i} reftype: {tables[i].RefType} limits: {tables[i].Min}, {tables[i].Max} {limitsType}");
            }
        }

        Element[]? elements;
        void ReadElementSection()
        {
            UInt32 count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            elements = new Element[count];
            for (uint i = 0; i < count; i++)
            {
                elements[i].Flags = (ElementFlag)Reader.ReadByte();
                if (Program.Verbose2)
                    Console.WriteLine($"  element: {i} flags: {elements[i].Flags}");

                if (elements[i].HasTableIdx)
                    elements[i].TableIdx = ReadU32();

                if (elements[i].HasExpression)
                {
                    (elements[i].Expression, _) = ReadBlock();
                    if (Program.Verbose2)
                    {
                        Console.WriteLine("  expression:");
                        foreach (var instruction in elements[i].Expression)
                            Console.WriteLine(instruction.ToString(this).Indent("    "));
                    }
                }

                if (elements[i].HasExpressions)
                {
                    if (elements[i].HasRefType)
                        elements[i].RefType = (ReferenceType)Reader.ReadByte();

                    var size = ReadU32();
                    elements[i].Expressions = new Instruction[size][];
                    for (uint j = 0; j < size; j++)
                    {
                        (elements[i].Expressions[j], _) = ReadBlock();
                    }
                }
                else
                {
                    if (elements[i].HasElemKind)
                        elements[i].Kind = Reader.ReadByte();

                    var size = ReadU32();
                    if (Program.Verbose2)
                        Console.WriteLine($"  size: {size}");

                    elements[i].Indices = new UInt32[size];
                    for (uint j = 0; j < size; j++)
                    {
                        elements[i].Indices[j] = ReadU32();

                        if (Program.Verbose2)
                            Console.WriteLine($"    idx[{j}] = {elements[i].Indices[j]}");
                    }
                }
            }
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

            for (uint i = 0; i < count; i++)
            {
                funcsCode[i].Idx = i;
                funcsCode[i].Size = ReadU32();
                funcsCode[i].Offset = Reader.BaseStream.Position;
                Reader.BaseStream.Seek(funcsCode[i].Offset + funcsCode[i].Size, SeekOrigin.Begin);
            }
        }

        BlockType ReadBlockType()
        {
            BlockType blockType = new();
            byte b = Reader.ReadByte();

            switch (b)
            {
                case 0x40:
                    blockType.Kind = BlockTypeKind.Empty;
                    break;
                case (byte)NumberType.f32:
                case (byte)NumberType.i32:
                case (byte)NumberType.f64:
                case (byte)NumberType.i64:
                case (byte)ReferenceType.ExternRef:
                case (byte)ReferenceType.FuncRef:
                    blockType.Kind = BlockTypeKind.ValueType;
                    Reader.BaseStream.Seek(-1, SeekOrigin.Current);
                    ReadValueType(ref blockType.ValueType);
                    break;
                default:
                    blockType.Kind = BlockTypeKind.TypeIdx;
                    Reader.BaseStream.Seek(-1, SeekOrigin.Current);
                    blockType.TypeIdx = (UInt32)ReadI64();
                    break;
            }

            return blockType;
        }

        MemArg ReadMemArg()
        {
            MemArg ma = new();

            ma.Align = ReadU32();
            ma.Offset = ReadU32();

            return ma;
        }

        public void DumpBytes(int count)
        {
            Console.WriteLine("bytes");

            for (int i = 0; i < count; i++)
            {
                Console.Write($" {Reader.ReadByte():x}");
            }

            Console.WriteLine();
        }

        Instruction ReadInstruction(Opcode opcode)
        {
            Instruction instruction = new() { Offset = Reader.BaseStream.Position - 1 };
            instruction.Opcode = opcode;
            Opcode op;

            switch (opcode)
            {
                case Opcode.Block:
                case Opcode.Loop:
                case Opcode.If:
                    //DumpBytes(16);
                    instruction.BlockType = ReadBlockType();

                    //Console.WriteLine($"if blocktype: {instruction.BlockType.Kind}");

                    (instruction.Block, op) = ReadBlock(opcode == Opcode.If ? Opcode.Else : Opcode.End);
                    if (op == Opcode.Else)
                        (instruction.Block2, _) = ReadBlock();
                    break;
                case Opcode.Memory_Size:
                case Opcode.Memory_Grow:
                    op = (Opcode)Reader.ReadByte();
                    if (op != Opcode.Unreachable)
                        throw new Exception($"0x00 expected after opcode: {opcode}, got {op} instead");
                    break;
                case Opcode.Call:
                case Opcode.Local_Get:
                case Opcode.Local_Set:
                case Opcode.Local_Tee:
                case Opcode.Global_Get:
                case Opcode.Global_Set:
                case Opcode.Br:
                case Opcode.Br_If:
                    instruction.Idx = ReadU32();
                    break;
                case Opcode.Br_Table:
                    var count = ReadU32();

                    instruction.IdxArray = new UInt32[count];

                    for (var i = 0; i < count; i++)
                        instruction.IdxArray[i] = ReadU32();

                    instruction.Idx = ReadU32();

                    break;
                case Opcode.Call_Indirect:
                    instruction.Idx = ReadU32();
                    instruction.Idx2 = ReadU32();
                    break;
                case Opcode.I32_Const:
                    instruction.I32 = ReadI32();
                    break;
                case Opcode.F32_Const:
                    instruction.F32 = Reader.ReadSingle();
                    break;
                case Opcode.I64_Const:
                    instruction.I64 = ReadI64();
                    break;
                case Opcode.F64_Const:
                    instruction.F64 = Reader.ReadDouble();
                    break;
                case Opcode.I32_Load:
                case Opcode.I64_Load:
                case Opcode.F32_Load:
                case Opcode.F64_Load:
                case Opcode.I32_Load8_S:
                case Opcode.I32_Load8_U:
                case Opcode.I32_Load16_S:
                case Opcode.I32_Load16_U:
                case Opcode.I64_Load8_S:
                case Opcode.I64_Load8_U:
                case Opcode.I64_Load16_S:
                case Opcode.I64_Load16_U:
                case Opcode.I64_Load32_S:
                case Opcode.I64_Load32_U:
                case Opcode.I32_Store:
                case Opcode.I64_Store:
                case Opcode.F32_Store:
                case Opcode.F64_Store:
                case Opcode.I32_Store8:
                case Opcode.I32_Store16:
                case Opcode.I64_Store8:
                case Opcode.I64_Store16:
                case Opcode.I64_Store32:
                    instruction.MemArg = ReadMemArg();
                    break;
                case Opcode.Unreachable:
                case Opcode.Nop:
                case Opcode.Return:
                case Opcode.Drop:
                case Opcode.Select:
                case Opcode.I32_Eqz:
                case Opcode.I32_Eq:
                case Opcode.I32_Ne:
                case Opcode.I32_Lt_S:
                case Opcode.I32_Lt_U:
                case Opcode.I32_Gt_S:
                case Opcode.I32_Gt_U:
                case Opcode.I32_Le_S:
                case Opcode.I32_Le_U:
                case Opcode.I32_Ge_S:
                case Opcode.I32_Ge_U:
                case Opcode.I64_Eqz:
                case Opcode.I64_Eq:
                case Opcode.I64_Ne:
                case Opcode.I64_Lt_S:
                case Opcode.I64_Lt_U:
                case Opcode.I64_Gt_S:
                case Opcode.I64_Gt_U:
                case Opcode.I64_Le_S:
                case Opcode.I64_Le_U:
                case Opcode.I64_Ge_S:
                case Opcode.I64_Ge_U:
                case Opcode.F32_Eq:
                case Opcode.F32_Ne:
                case Opcode.F32_Lt:
                case Opcode.F32_Gt:
                case Opcode.F32_Le:
                case Opcode.F32_Ge:
                case Opcode.F64_Eq:
                case Opcode.F64_Ne:
                case Opcode.F64_Lt:
                case Opcode.F64_Gt:
                case Opcode.F64_Le:
                case Opcode.F64_Ge:
                case Opcode.I32_Clz:
                case Opcode.I32_Ctz:
                case Opcode.I32_Popcnt:
                case Opcode.I32_Add:
                case Opcode.I32_Sub:
                case Opcode.I32_Mul:
                case Opcode.I32_Div_S:
                case Opcode.I32_Div_U:
                case Opcode.I32_Rem_S:
                case Opcode.I32_Rem_U:
                case Opcode.I32_And:
                case Opcode.I32_Or:
                case Opcode.I32_Xor:
                case Opcode.I32_Shl:
                case Opcode.I32_Shr_S:
                case Opcode.I32_Shr_U:
                case Opcode.I32_Rotl:
                case Opcode.I32_Rotr:
                case Opcode.I64_Clz:
                case Opcode.I64_Ctz:
                case Opcode.I64_Popcnt:
                case Opcode.I64_Add:
                case Opcode.I64_Sub:
                case Opcode.I64_Mul:
                case Opcode.I64_Div_S:
                case Opcode.I64_Div_U:
                case Opcode.I64_Rem_S:
                case Opcode.I64_Rem_U:
                case Opcode.I64_And:
                case Opcode.I64_Or:
                case Opcode.I64_Xor:
                case Opcode.I64_Shl:
                case Opcode.I64_Shr_S:
                case Opcode.I64_Shr_U:
                case Opcode.I64_Rotl:
                case Opcode.I64_Rotr:
                case Opcode.F32_Abs:
                case Opcode.F32_Neg:
                case Opcode.F32_Ceil:
                case Opcode.F32_Floor:
                case Opcode.F32_Trunc:
                case Opcode.F32_Nearest:
                case Opcode.F32_Sqrt:
                case Opcode.F32_Add:
                case Opcode.F32_Sub:
                case Opcode.F32_Mul:
                case Opcode.F32_Div:
                case Opcode.F32_Min:
                case Opcode.F32_Max:
                case Opcode.F32_Copysign:
                case Opcode.F64_Abs:
                case Opcode.F64_Neg:
                case Opcode.F64_Ceil:
                case Opcode.F64_Floor:
                case Opcode.F64_Trunc:
                case Opcode.F64_Nearest:
                case Opcode.F64_Sqrt:
                case Opcode.F64_Add:
                case Opcode.F64_Sub:
                case Opcode.F64_Mul:
                case Opcode.F64_Div:
                case Opcode.F64_Min:
                case Opcode.F64_Max:
                case Opcode.F64_Copysign:
                case Opcode.I32_Wrap_I64:
                case Opcode.I32_Trunc_F32_S:
                case Opcode.I32_Trunc_F32_U:
                case Opcode.I32_Trunc_F64_S:
                case Opcode.I32_Trunc_F64_U:
                case Opcode.I64_Extend_I32_S:
                case Opcode.I64_Extend_I32_U:
                case Opcode.I64_Trunc_F32_S:
                case Opcode.I64_Trunc_F32_U:
                case Opcode.I64_Trunc_F64_S:
                case Opcode.I64_Trunc_F64_U:
                case Opcode.F32_Convert_I32_S:
                case Opcode.F32_Convert_I32_U:
                case Opcode.F32_Convert_I64_S:
                case Opcode.F32_Convert_I64_U:
                case Opcode.F32_Demote_F64:
                case Opcode.F64_Convert_I32_S:
                case Opcode.F64_Convert_I32_U:
                case Opcode.F64_Convert_I64_S:
                case Opcode.F64_Convert_I64_U:
                case Opcode.F64_Promote_F32:
                case Opcode.I32_Reinterpret_F32:
                case Opcode.I64_Reinterpret_F64:
                case Opcode.F32_Reinterpret_I32:
                case Opcode.F64_Reinterpret_I64:
                    break;

                default:
                    throw new FileLoadException($"Unknown opcode: {opcode} ({opcode:x})");
            }

            return instruction;
        }

        public (Instruction[], Opcode) ReadBlock(Opcode end = Opcode.End)
        {
            List<Instruction> instructions = new();
            Opcode b;
            do
            {
                b = (Opcode)Reader.ReadByte();
                if (b == Opcode.End || b == end)
                    break;

                //Console.WriteLine($"    opcode: {b}");

                instructions.Add(ReadInstruction(b));
            } while (true);

            return (instructions.ToArray(), b);
        }

        List<string> customSectionNames = new();

        void ReadCustomSection(UInt32 size)
        {
            var start = Reader.BaseStream.Position;
            var name = ReadString();
            customSectionNames.Add(name);

            if (Program.Verbose)
                Console.Write($" name: {name}");

            if (name == "name")
            {
                ReadCustomNameSection(size - (UInt32)(Reader.BaseStream.Position - start));
            }
        }

        string moduleName = "";
        readonly NameMap functionNames = new();
        readonly Dictionary<string, UInt32> nameToFunction = new();
        readonly NameMap globalNames = new();
        readonly NameMap dataSegmentNames = new();
        readonly Dictionary<UInt32, NameMap> localNames = new();

        void ReadCustomNameSection(UInt32 size)
        {
            var start = Reader.BaseStream.Position;

            if (Program.Verbose2)
                Console.WriteLine();

            while (Reader.BaseStream.Position - start < size)
            {
                var id = (CustomSubSectionId)Reader.ReadByte();
                UInt32 subSectionSize = ReadU32();
                var subSectionStart = Reader.BaseStream.Position;

                switch (id)
                {
                    case CustomSubSectionId.ModuleName:
                        moduleName = ReadString();
                        if (Program.Verbose2)
                            Console.WriteLine($"  module name: {moduleName}");
                        break;
                    case CustomSubSectionId.FunctionNames:
                        ReadNameMap(functionNames, "function", nameToFunction);
                        break;
                    case CustomSubSectionId.LocalNames:
                        ReadIndirectNameMap(localNames, "local", "function");
                        break;
                    case CustomSubSectionId.GlobalNames:
                        ReadNameMap(globalNames, "global");
                        break;
                    case CustomSubSectionId.DataSegmentNames:
                        ReadNameMap(dataSegmentNames, "data segment");
                        break;
                    default:
                        if (Program.Verbose2)
                            Console.WriteLine($"  subsection {id}");
                        break;
                }

                Reader.BaseStream.Seek(subSectionStart + subSectionSize, SeekOrigin.Begin);
            }
        }

        void ReadIndirectNameMap(Dictionary<UInt32, NameMap> indirectMap, string mapName, string indiceName)
        {
            var count = ReadU32();
            if (Program.Verbose2)
                Console.WriteLine($"  {mapName} names count: {count}");

            for (int i = 0; i < count; i++)
            {
                var idx = ReadU32();
                if (Program.Verbose2)
                    Console.WriteLine($"    {mapName} map for {indiceName}: {idx}");

                var map = ReadNameMap(null, mapName);
                if (indirectMap.ContainsKey(idx))
                    Console.WriteLine($"\nwarning: duplicate {indiceName} idx: {idx} in {mapName} names indirect map ignored");
                else
                    indirectMap[idx] = map;
            }
        }

        Dictionary<UInt32, string> ReadNameMap(Dictionary<UInt32, string>? map, string mapName, Dictionary<string, UInt32>? reversed = null)
        {
            var count = ReadU32();
            if (Program.Verbose2)
                Console.WriteLine($"      {mapName} names count: {count}");

            if (map == null)
                map = new Dictionary<UInt32, string>();

            for (int i = 0; i < count; i++)
            {
                var idx = ReadU32();
                var name = ReadString();
                if (Program.Verbose2)
                    Console.WriteLine($"        {mapName} idx: {idx} name: {name}");

                if (map.ContainsKey(idx))
                    Console.WriteLine($"\nwarning: duplicate {mapName} idx: {idx} = '{name}' in {mapName} names map ignored");
                else
                {
                    map[idx] = name;
                    if (reversed != null)
                        reversed[name] = idx;
                }
            }

            return map;
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
                exports[i].Desc = (ExportDesc)Reader.ReadByte();
                exports[i].Idx = ReadU32();

                if (Program.Verbose2)
                    Console.WriteLine($"  {exports[i]}");
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
                imports[i].Desc = (ImportDesc)Reader.ReadByte();
                imports[i].Idx = ReadU32();

                if (Program.Verbose2)
                    Console.WriteLine($"  {imports[i]}");
            }
        }

        string ReadString()
        {
            return Encoding.UTF8.GetString(Reader.ReadBytes((int)ReadU32()));
        }

        Function[]? functions;

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
                var b = Reader.ReadByte();
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

        public void ReadValueType(ref ValueType vt)
        {
            var b = Reader.ReadByte();
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

        public UInt32 ReadU32()
        {
            UInt32 value = 0;
            var offset = 0;
            do
            {
                var b = Reader.ReadByte();
                value |= (UInt32)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    break;

                offset += 7;
            } while (true);

            return value;
        }

        Int32 ReadI32()
        {
            Int32 value = 0;
            var offset = 0;
            byte b;

            do
            {
                b = Reader.ReadByte();
                value |= (Int32)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    break;

                offset += 7;
            } while (true);

            if (offset < 32 && (b & 0x40) == 0x40)
                value |= (~(Int32)0 << offset);

            return value;
        }

        Int64 ReadI64()
        {
            Int64 value = 0;
            var offset = 0;
            byte b;

            do
            {
                b = Reader.ReadByte();
                value |= (Int64)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    break;

                offset += 7;
            } while (true);

            if (offset < 64 && (b & 0x40) == 0x40)
                value |= (~(Int64)0 << offset);

            return value;
        }

        public void PrintFunctions()
        {
            if (functions == null)
                return;

            for (UInt32 idx = 0; idx < functions.Length; idx++)
            {
                string? name = null;

                if (Program.FunctionFilter != null)
                {
                    if (functionNames == null || functionNames.Count < 1)
                        continue;

                    name = functionNames[FunctionOffset(idx)];
                    if (name == null)
                        continue;

                    if (!Program.FunctionFilter.Match(name).Success)
                        continue;
                }
                else
                    name = FunctionName(FunctionOffset(idx));

                PrintFunction(idx, name);
            }
        }

        void PrintFunction(UInt32 idx, string? name)
        {
            if (functions == null || functionTypes == null || funcsCode == null)
                return;

            if (string.IsNullOrEmpty(name))
                name = $"idx:{idx}";

            Console.WriteLine($"{functionTypes[functions[idx].TypeIdx].ToString(name)}\n{funcsCode[idx].ToString(this)}");
        }

        UInt32 FunctionOffset(UInt32 idx)
        {
            return (imports == null ? 0 : (UInt32)imports.Length) + idx;
        }

        public string FunctionName(UInt32 idx)
        {
            return functionNames[idx];
        }
        public string FunctionType(UInt32 idx)
        {
            if (functionTypes == null)
                return string.Empty;

            return functionTypes[idx].ToString();
        }

        public string GlobalName(UInt32 idx)
        {
            return globalNames[idx];
        }

        public void FindFunctionsCallingInterp()
        {
            if (funcsCode == null || imports == null)
                return;

            if (!nameToFunction.TryGetValue("mini_llvmonly_get_interp_entry", out var interpIdx))
            {
                Console.WriteLine("Unable to find `mini_llvmonly_get_interp_entry` function. Make sure the wasm is built with AOT and native debug symbols enabled.");

                return;
            }

            uint count = 0, totalCount = 0;
            for (UInt32 idx = 0; idx < funcsCode.Length; idx++)
            {
                UInt32 funcIdx = idx + (UInt32)imports.Length;
                var name = functionNames[funcIdx];
                if (Program.FunctionFilter != null && !Program.FunctionFilter.Match(name).Success)
                    continue;

                totalCount++;

                if (FunctionCallsFunction(funcIdx, interpIdx))
                {
                    count++;

                    if (Program.Verbose)
                        Console.WriteLine($"function {name} calls interpreter, code size: {funcsCode[idx].Size}");
                }
            }

            Console.WriteLine($"AOT stats: {count} function(s) call(s) interpreter, {(totalCount == 0 ? 0 : ((double)100 * count) / totalCount):N2}% of {totalCount} functions");
        }

        bool FunctionCallsFunction(UInt32 idx, UInt32 calledIdx)
        {
            if (funcsCode == null || imports == null)
                return false;

            var code = funcsCode[idx - imports.Length];
            if (!code.EnsureCodeReaded(this))
                return false;

            foreach (var inst in code.Instructions)
            {
                if (inst.Opcode == Opcode.Call && inst.Idx == calledIdx)
                    return true;
            }

            return false;
        }

        public void PrintSummary()
        {
            var moduleName = string.IsNullOrEmpty(this.moduleName) ? null : $" name: {this.moduleName}";
            Console.WriteLine($"Module:{moduleName} path: {Path}");
            Console.WriteLine($"  size: {Reader.BaseStream.Length:N0}");
            Console.WriteLine($"  binary format version: {Version}");
            Console.WriteLine($"  sections: {sections.Count}");

            int customSectionOffset = 0;
            for (int i = 0; i < sections.Count; i++)
            {
                var sectionName = (sections[i].id == SectionId.Custom && customSectionOffset < customSectionNames.Count) ? $" name: {customSectionNames[customSectionOffset++]}" : "";
                Console.WriteLine($"    id: {sections[i].id}{sectionName} size: {sections[i].size:N0}");
            }
        }
    }
}
