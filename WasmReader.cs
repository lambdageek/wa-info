using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NameMap = System.Collections.Generic.Dictionary<System.UInt32, string>;

namespace WebAssemblyInfo
{
    class WasmReader : WasmReaderBase, IWasmReaderContext
    {
        public WasmReader(string path) : base(path)
        {
        }

        override protected IWasmReaderContext? Context => this;
        override protected void ReadSection(SectionInfo section)
        {
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
                case SectionId.Data:
                    ReadDataSection();
                    break;
                case SectionId.Global:
                    ReadGlobalSection();
                    break;
                case SectionId.Memory:
                    ReadMemorySection();
                    break;
                default:
                    break;
            }
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
                        {
                            Console.WriteLine(instruction.ToString(this).Indent("    "));
                    }
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

        Data[]? dataSegments;
        void ReadDataSection()
        {
            var count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            dataSegments = new Data[count];
            for (uint i = 0; i < count; i++)
            {
                if (Program.Verbose2)
                    Console.Write($"  data idx: {i}");

                ReadDataSegment(ref dataSegments[i]);
            }
        }

        Global[]? globals;
        void ReadGlobalSection()
        {
            var count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            globals = new Global[count];
            for (uint i = 0; i < count; i++)
            {
                if (Program.Verbose2)
                    Console.Write($"  global idx: {i}");

                ReadGlobal(ref globals[i]);

                if (Program.Verbose2)
                    Console.WriteLine();
            }
        }

        Memory[]? memories;
        void ReadMemorySection()
        {
            var count = ReadU32();

            if (Program.Verbose)
                Console.Write($" count: {count}");

            if (Program.Verbose2)
                Console.WriteLine();

            memories = new Memory[count];
            for (uint i = 0; i < count; i++)
            {
                var limitsType = Reader.ReadByte();
                memories[i].Min = ReadU32();
                memories[i].Max = limitsType == 1 ? ReadU32() : UInt32.MaxValue;

                if (Program.Verbose2)
                    Console.Write($"  memory: {i} limits: {memories[i].Min}, {memories[i].Max} has max: {limitsType == 1}");

                if (Program.Verbose2)
                    Console.WriteLine();
            }
        }

        protected Code[]? funcsCode;
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

        public void DumpBytes(int count)
        {
            var pos = Reader.BaseStream.Position;
            Console.WriteLine("bytes");

            for (int i = 0; i < count; i++)
            {
                Console.Write($" {Reader.ReadByte():x}");
            }

            Console.WriteLine();
            Reader.BaseStream.Position = pos;
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
                ReadExport(ref exports[i]);
            }
        }

        Import[]? imports;
        uint functionImportsCount = 0;

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

                if (imports[i].Desc == ImportDesc.TypeIdx)
                    functionImportsCount++;

                if (Program.Verbose2)
                    Console.WriteLine($"  {imports[i]}");
            }
        }

        protected Function[]? functions;

        protected FunctionType[]? functionTypes;

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

        public bool HasFunctionNames { get { return functionNames != null && functionNames.Count > 0; } }

        public string GetFunctionName(UInt32 idx, bool needsOffset = true)
        {
            string? name = HasFunctionNames ? functionNames[needsOffset ? FunctionOffset(idx) : idx] : null;
            if (string.IsNullOrEmpty(name))
                name = $"idx:{idx}";

            return name;
        }

        public bool GetFunctionIdx(string name, out UInt32 idx)
        {
            if (!nameToFunction.ContainsKey(name))
            {
                idx = 0;
                return false;
            }

            idx = nameToFunction[name] - functionImportsCount;

            return true;
        }

        protected delegate void ProcessFunction(UInt32 idx, string? name, object? data);

        protected void FilterFunctions(ProcessFunction processFunction, object? data = null)
        {
            if (functions == null)
                return;

            for (UInt32 idx = 0; idx < functions.Length; idx++)
            {
                string? name = null;
                bool process = Program.FunctionFilter == null && Program.FunctionOffset == -1;

                if (Program.FunctionOffset != -1 && funcsCode != null
                    && idx < funcsCode.Length
                    && funcsCode[idx].Offset <= Program.FunctionOffset && funcsCode[idx].Offset + funcsCode[idx].Size > Program.FunctionOffset)
                {
                    process = true;
                }

                if (!process && Program.FunctionFilter != null)
                {
                    if (!HasFunctionNames)
                        continue;

                    name = GetFunctionName(idx);
                    if (name == null)
                        continue;

                    if (!Program.FunctionFilter.Match(name).Success)
                        continue;

                    process = true;
                }
                else if (process)
                {
                    name = GetFunctionName(idx);
                }

                if (!process)
                    continue;

                processFunction(idx, name, data);
            }
        }

        public void PrintFunctions()
        {
            FilterFunctions(PrintFunction);
        }

        protected void PrintFunctionWithPrefix(UInt32 idx, string? name, string? prefix = null)
        {
            if (functions == null || functionTypes == null || funcsCode == null)
                return;

            //Console.WriteLine($"read func {name}");
            var type = functionTypes[functions[idx].TypeIdx];
            EnsureCodeReaded(ref funcsCode[idx]);
            Console.WriteLine($"{prefix}{type.ToString(name, true)}\n{funcsCode[idx].ToString(this, type.Parameters.Types.Length).Indent(prefix)}");
        }

        protected void PrintFunction(UInt32 idx, string? name, object? _ = null)
        {
            PrintFunctionWithPrefix(idx, name, null);
        }

        UInt32 FunctionOffset(UInt32 idx) => functionImportsCount + idx;

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
            return (globalNames != null && globalNames.ContainsKey(idx)) ? globalNames[idx] : $"global:{idx}";
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
                UInt32 funcIdx = FunctionOffset(idx);
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

            var code = funcsCode[idx - functionImportsCount];
            if (!EnsureCodeReaded(ref code))
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
                var id = sections[i].id;
                var sectionName = (id == SectionId.Custom && customSectionOffset < customSectionNames.Count) ? $" name: {customSectionNames[customSectionOffset++]}" : "";
                Console.WriteLine($"    id: {id}{sectionName} size: {sections[i].size:N0}");
            }
        }
    }
}
