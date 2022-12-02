using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebAssemblyInfo
{
    internal class WasmRewriter : WasmReaderBase
    {
        readonly string DestinationPath;
        BinaryWriter Writer;
        WasmWriterUtils WriterUtils { get; }

        public WasmRewriter(string source, string destination) : base(source)
        {
            if (Program.Verbose)
                Console.WriteLine($"Writing wasm file: {destination}");

            DestinationPath = destination;
            var stream = File.Open(DestinationPath, FileMode.Create);
            Writer = new BinaryWriter(stream);
            WriterUtils = new WasmWriterUtils(Writer);
        }

        protected override void ReadModule()
        {
            WriterUtils.WriteWasmHeader(version: 1);

            base.ReadModule();

            Writer.BaseStream.Position = MagicWasm.Length;
            Writer.Write(Version);
        }

        override protected void ReadSection(SectionInfo section)
        {
            if (File.Exists(Program.DataSectionFile))
            {
                if (section.id == SectionId.Data)
                {
                    RewriteDataSection();
                    return;
                }

                if (section.id == SectionId.DataCount)
                {
                    // omit DataCount section for now, it is not needed
                    return;
                }
            }

            WriteSection(section);
        }

        void WriteSection(SectionInfo section)
        {
            Reader.BaseStream.Seek(section.offset, SeekOrigin.Begin);
            Writer.Write(Reader.ReadBytes((int)section.size + (int)(section.begin - section.offset)));
        }

        struct Chunk
        {
            public int index, size;
        }

        List<Chunk> Split(byte[] data)
        {
            int zeroesLen = 9;
            var list = new List<Chunk>();
            var span = new ReadOnlySpan<byte>(data);
            var zeroes = new ReadOnlySpan<byte>(new byte[zeroesLen]);
            int offset = 0;
            int stripped = 0;

            do
            {
                int index = span.IndexOf(zeroes);
                if (index == -1)
                {
                    if (Program.Verbose2)
                        Console.WriteLine($"  add last idx: {offset} size: {data.Length - offset} span remaining len: {span.Length}");

                    list.Add(new Chunk { index = offset, size = data.Length - offset });
                    return list;
                }
                if (index != 0)
                {
                    if (Program.Verbose2)
                        Console.WriteLine($"  add idx: {offset} size: {index} span remaining len: {span.Length} span index: {index}");

                    list.Add(new Chunk { index = offset, size = index });
                    span = span.Slice(index + zeroesLen);
                    offset += index + zeroesLen;
                    stripped += zeroesLen;
                }
                index = span.IndexOfAnyExcept((byte)0);
                if (index == -1)
                {
                    stripped += data.Length - offset;
                    break;
                }

                //Console.WriteLine($"skip: {index}");
                if (index != 0)
                {
                    span = span.Slice(index);
                    offset += index;
                    stripped += index;
                }
            } while (true);

            if (Program.Verbose)
                Console.Write($"    segments detected: {list.Count:N0} zero bytes stripped: {stripped:N0}");

            return list;
        }

        void RewriteDataSection()
        {
            //var oo = Writer.BaseStream.Position;
            var bytes = File.ReadAllBytes(Program.DataSectionFile);
            var chunk = new Chunk { index = 0, size = bytes.Length };
            var segments = Program.DataSectionAutoSplit ? Split(bytes) : new List<Chunk> { chunk };

            var mode = Program.DataSectionMode;
            var sectionLen = U32Len((uint)segments.Count);
            foreach (var segment in segments)
                sectionLen += GetDataSegmentLength(mode, segment, Program.DataOffset + segment.index);

            // section beginning
            Writer.Write((byte)SectionId.Data);
            WriteU32(sectionLen);

            // section content
            WriteU32((uint)segments.Count);
            foreach (var segment in segments)
                WriteDataSegment(mode, bytes, segment, Program.DataOffset + segment.index);

            //var pos = Writer.BaseStream.Position;
            //Writer.BaseStream.Position = oo;
            //DumpBytes(64);
            //Writer.BaseStream.Position = pos;
        }

        uint GetDataSegmentLength(DataMode mode, Chunk chunk, int memoryOffset)
        {
            var len = U32Len((uint)mode) + U32Len((uint)chunk.size) + (uint)chunk.size;
            if (mode == DataMode.Active)
                len += ConstI32ExprLen(memoryOffset);

            return len;
        }

        void WriteDataSegment(DataMode mode, byte[] data, Chunk chunk, int memoryOffset)
        => WriterUtils.WriteDataSegment(mode, new ReadOnlySpan<byte>(data, chunk.index, chunk.size), memoryOffset);

        public void DumpBytes(int count)
        {
            var pos = Writer.BaseStream.Position;
            Console.WriteLine("bytes");

            for (int i = 0; i < count; i++)
            {
                Console.Write($" {Writer.BaseStream.ReadByte():x}");
            }

            Console.WriteLine();
            Writer.BaseStream.Position = pos;
        }

        public uint ConstI32ExprLen(int value) => WasmWriterUtils.ConstI32ExprLen(value);

        // i32.const <cn>
        void WriteConstI32Expr(int cn) => WriterUtils.WriteConstI32Expr(cn);

        void WriteU32(uint n) => WriterUtils.WriteU32(n);

        static uint U32Len(uint n) => WasmWriterUtils.U32Len(n);

        void WriteI32(int n) => WriterUtils.WriteI32(n);

        static uint I32Len(int n) => WasmWriterUtils.I32Len(n);
    }
}
