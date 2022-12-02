using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NameMap = System.Collections.Generic.Dictionary<System.UInt32, string>;

namespace WebAssemblyInfo
{
    public abstract partial class WasmReaderBase : IDisposable
    {
        public readonly BinaryReader Reader;
        public UInt32 Version { get; private set; }
        public string Path { get; private set; }

        public WasmReaderBase(string path)
        {
            if (Program.Verbose)
                Console.WriteLine($"Reading wasm file: {path}");

            Path = path;
            var stream = File.Open(Path, FileMode.Open);
            Reader = new BinaryReader(stream);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                Reader.Dispose();
        }

        public void Parse()
        {
            ReadModule();
        }

        internal static readonly byte[] MagicWasm = { 0x0, 0x61, 0x73, 0x6d };

        protected virtual void ReadModule()
        {
            var magicBytes = Reader.ReadBytes(4);

            for (int i = 0; i < MagicWasm.Length; i++)
            {
                if (MagicWasm[i] != magicBytes[i])
                    throw new FileLoadException("not wasm file, module magic is wrong");
            }

            Version = Reader.ReadUInt32();
            if (Program.Verbose)
                Console.WriteLine($"WebAssembly binary format version: {Version}");

            while (Reader.BaseStream.Position < Reader.BaseStream.Length)
                ReadSection();
        }

        public enum SectionId
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
            DataCount,
            Tag,
        }

        public struct SectionInfo
        {
            public SectionId id;
            public UInt32 size;
            public long offset;
            public long begin;
        }
        protected List<SectionInfo> sections = new();
        protected Dictionary<SectionId, List<SectionInfo>> sectionsById = new();

        protected abstract void ReadSection(SectionInfo section);

        protected virtual IWasmReaderContext? Context => null;

        void ReadSection()
        {
            var section = new SectionInfo() { offset=Reader.BaseStream.Position, id = (SectionId)Reader.ReadByte(), size = ReadU32(), begin = Reader.BaseStream.Position };
            sections.Add(section);
            if (!sectionsById.ContainsKey(section.id))
                sectionsById[section.id] = new List<SectionInfo>();

            sectionsById[section.id].Add(section);

            if (Program.Verbose)
                Console.Write($"Reading section: {section.id,9} size: {section.size,12}");

            ReadSection(section);

            if (Program.Verbose)
                Console.WriteLine();

            Reader.BaseStream.Seek(section.begin + section.size, SeekOrigin.Begin);
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

        protected Int32 ReadI32()
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

        protected Int64 ReadI64()
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

        private protected (Instruction[], Opcode) ReadBlock(Opcode end = Opcode.End)
        {
            List<Instruction> instructions = new();
            Opcode b;
            do
            {
                b = (Opcode)Reader.ReadByte();
                //Console.WriteLine($"    opcode: {b}");

                if (b == Opcode.End || b == end)
                    break;

                instructions.Add(ReadInstruction(b));
            } while (true);

            return (instructions.ToArray(), b);
        }

        private protected void ReadCode(ref Code code)
        {
            Reader.BaseStream.Seek(code.Offset, SeekOrigin.Begin);

            if (Program.Verbose2)
                Console.WriteLine($"  code[{code.Idx}]: {code.Size} bytes");

            var vecSize = ReadU32();
            code.Locals = new LocalsBlock[vecSize];

            if (Program.Verbose2)
                Console.WriteLine($"    locals blocks count {vecSize}");

            for (var j = 0; j < vecSize; j++)
            {
                code.Locals[j].Count = ReadU32();
                ReadValueType(ref code.Locals[j].Type);

                // Console.WriteLine($"    locals {j} count: {Locals[j].Count} type: {Locals[j].Type}");
            }

            // read expr
            (code.Instructions, _) = ReadBlock();

            if (Program.Verbose2)
                Console.WriteLine(code.ToString().Indent("    "));
        }

        private protected bool EnsureCodeReaded(ref Code code)
        {
            if (code.Instructions == null)
            {
                ReadCode(ref code);
            }
            return true;
        }
        private protected void ReadGlobal(ref Global g)
        {
            ReadValueType(ref g.Type);
            if (Program.Verbose2)
                Console.Write($" type: {g.Type}");

            g.Mutability = (Mutability)Reader.ReadByte();
            if (Program.Verbose2)
                Console.Write($" mutability: {g.Mutability.ToString().ToLower()}");

            (g.Expression, _) = ReadBlock();

            if (Program.Verbose2)
            {
                if (g.Expression.Length == 1)
                {
                    Console.Write($" init expression: {g.Expression[0]}");
                }
                else
                {
                    Console.WriteLine(" init expression:");
                    foreach (var instruction in g.Expression)
                        Console.Write(instruction.ToString(Context).Indent("    "));
                }
            }
        }

        private protected void ReadValueType(ref ValueType vt)
        {
            var b = Reader.ReadByte();
            vt.IsRefenceType = b <= 0x70;
            vt.IsVectorType = b == 0x7b;
            vt.value = b;
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

        protected string ReadString()
        {
            return Encoding.UTF8.GetString(Reader.ReadBytes((int)ReadU32()));
        }

        private protected void ReadExport(ref Export export)
        {
            export.Name = ReadString();
            export.Desc = (ExportDesc)Reader.ReadByte();
            export.Idx = ReadU32();

            if (Program.Verbose2)
                Console.WriteLine($"  {export}");
        }

        private protected void ReadDataSegment(ref Data dataSegment)
        {
            dataSegment.Mode = (DataMode)ReadU32();
            if (Program.Verbose2)
                Console.Write($" mode: {dataSegment.Mode}");
            switch (dataSegment.Mode)
            {
                case DataMode.ActiveMemory:
                    dataSegment.MemIdx = ReadU32();
                    if (Program.Verbose2)
                        Console.Write($" memory index: {dataSegment.MemIdx}");
                    goto case DataMode.Active;
                case DataMode.Active:
                    (dataSegment.Expression, _) = ReadBlock();
                    if (Program.Verbose2)
                    {
                        Console.Write(" offset expression:");
                        if (dataSegment.Expression.Length == 1)
                        {
                            Console.Write($" {dataSegment.Expression[0]}");
                        }
                        else
                        {
                            Console.WriteLine();
                            foreach (var instruction in dataSegment.Expression)
                                Console.Write(instruction.ToString(Context).Indent("    "));
                        }
                    }
                    break;
            }

            var length = ReadU32();
            if (Program.Verbose2)
                Console.WriteLine($" length: {length}");

            dataSegment.Content = Reader.ReadBytes((int)length);

        }

    }
}
