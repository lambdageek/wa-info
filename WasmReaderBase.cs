﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NameMap = System.Collections.Generic.Dictionary<System.UInt32, string>;

namespace WebAssemblyInfo
{
    public abstract class WasmReaderBase
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

        public void Parse()
        {
            ReadModule();
        }

        protected byte[] MagicWasm = { 0x0, 0x61, 0x73, 0x6d };

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

        protected enum SectionId
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

        protected struct SectionInfo
        {
            public SectionId id;
            public UInt32 size;
            public long offset;
            public long begin;
        }
        protected List<SectionInfo> sections = new();
        protected Dictionary<SectionId, List<SectionInfo>> sectionsById = new();

        protected abstract void ReadSection(SectionInfo section);

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
    }
}
