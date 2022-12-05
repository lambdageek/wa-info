using System;
using System.Text;
using System.Collections.Generic;
using System.IO;

using WebAssemblyInfo.WebCIL;

namespace WebAssemblyInfo;

public sealed class TemplateWriter
{
    private readonly string _outputModulePath;
    private readonly TemplateReader _templateReader;

    private readonly BinaryWriter _writer;

    private readonly WasmWriterUtils WU;

    TemplateReader Reader => _templateReader;

    EmbeddingTemplate Template => Reader.Template;

    private Global[] _replacementGlobals;

    private Data[] _replacementDataSegments;

    private ImageDescriptor[] Images;

    public record ImageDescriptor(string Path, Stream Content);


    public TemplateWriter(BinaryWriter writer, string assemblyPath, Stream assemblyStream, TemplateReader templateReader)
    {
        _templateReader = templateReader;
        _writer = writer;
        WU = new WasmWriterUtils(writer);
        _replacementGlobals = default!;
        _replacementDataSegments = default!;
        Images = new ImageDescriptor[1];
        Images[0] = new ImageDescriptor(assemblyPath, assemblyStream);
    }

    public void ReplaceTemplateContent()
    {
        _replacementGlobals = ReplaceGlobals(Reader.Globals);
        _replacementDataSegments = ReplaceDataSegments(Reader.DataSegments);
    }

    private Global[] ReplaceGlobals(IReadOnlyCollection<Global> globals)
    {
        var newGlobals = new Global[globals.Count];
        foreach ((var global, var i) in globals.WithIndex())
        {
            if (i == Template.DescriptorLengthGlobalIdx.Index)
            {
                newGlobals[i] = ComputeDescriptorLengthGlobal();
            }
            else if (i == Template.Module0LengthGlobalIdx.Index)
            {
                newGlobals[i] = ComputeModule0Length();
            }
            else
            {
                var newExpr = new Instruction[global.Expression.Length];
                global.Expression.CopyTo(newExpr, 0);
                var newGlobal = new Global
                {
                    Type = global.Type,
                    Mutability = global.Mutability,
                    Expression = newExpr
                };
                newGlobals[i] = newGlobal;
            }
        }

        return newGlobals;
    }

    private Data[] ReplaceDataSegments(IReadOnlyCollection<Data> dataSegments)
    {
        var newDataSegments = new Data[dataSegments.Count];
        foreach ((var data, var i) in dataSegments.WithIndex())
        {
            if (i == Template.Module0DataIdx.Index)
            {
                newDataSegments[i] = ComputeModule0DataSegment();
            }
            else if (i == Template.DescriptorDataIdx.Index)
            {
                newDataSegments[i] = ComputeDescriptorDataSegment();
            }
            else
            {
                var newExpr = new Instruction[data.Expression.Length];
                data.Expression.CopyTo(newExpr, 0);
                var newContent = new byte[data.Content.Length];
                data.Content.CopyTo(newContent, 0);
                var newData = new Data
                {
                    Mode = data.Mode,
                    Expression = newExpr,
                    MemIdx = data.MemIdx,
                    Content = newContent
                };
                newDataSegments[i] = newData;
            }
        }

        return newDataSegments;
    }

    public void Write()
    {
        WU.WriteWasmHeader(version: 1);
        foreach (var section in Reader.Sections)
        {
            switch (section.id)
            {
                case WasmReaderBase.SectionId.Global:
                    WriteGlobalSection();
                    break;
                case WasmReaderBase.SectionId.Data:
                    WriteDataSection();
                    break;
                default:
                    WU.WriteSectionHeader(section.id, size: section.size);
                    WriteSection(section);
                    break;
            }
        }
    }

    private void WriteGlobalSection()
    {
        IReadOnlyCollection<Global> globals = _replacementGlobals;

        using var sectionStream = new MemoryStream();

        using (var sectionWriter = new BinaryWriter(sectionStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            var su = new WasmWriterUtils(sectionWriter);
            su.WriteU32((uint)globals.Count);
            foreach ((var global, var idx) in globals.WithIndex())
            {
                su.WriteGlobal(global);
            }
        }

        sectionStream.Flush();
        sectionStream.Seek(0, SeekOrigin.Begin);

        WU.WriteSectionHeader(WasmReaderBase.SectionId.Global, size: (uint)sectionStream.Length);
        sectionStream.CopyTo(_writer.BaseStream);
    }

    private void WriteDataSection()
    {
        IReadOnlyCollection<Data> segments = _replacementDataSegments;

        using var sectionStream = new MemoryStream();

        using (var sectionWriter = new BinaryWriter(sectionStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            var su = new WasmWriterUtils(sectionWriter);
            su.WriteU32((uint)segments.Count);
            foreach (var segment in segments)
            {
                // TODO: replace known data segments by our own
                su.WriteDataSegment(segment);
            }
        }

        sectionStream.Flush();
        sectionStream.Seek(0, SeekOrigin.Begin);
        WU.WriteSectionHeader(WasmReaderBase.SectionId.Data, size: (uint)sectionStream.Length);
        sectionStream.CopyTo(_writer.BaseStream);
    }

    private void WriteSection(WasmReaderBase.SectionInfo section)
    {
        Reader.Reader.BaseStream.Seek(section.begin, SeekOrigin.Begin);
        _writer.Write(Reader.Reader.ReadBytes((int)section.size));
    }

    private Global ComputeDescriptorLengthGlobal()
    {
        return ConstI32Global(ComputeDescriptorLength());
    }

    private Global ComputeModule0Length()
    {
        return ConstI32Global((uint)Images[0].Content.Length);
    }

    internal static Global ConstI32Global(uint n)
    {
        Global g = new Global
        {
            Type = new ValueType { Number = NumberType.i32 },
            Mutability = Mutability.Const,
            Expression = new Instruction[1] {
                new Instruction { Opcode = Opcode.I32_Const, I32 = (int)n }
                }
        };
        return g;
    }

    public uint ComputeDescriptorLength()
    {
        const uint imageCountLength = 4;

        const uint nameLengthLength = 4;
        string name = Images[0].Path;
        uint nameLength = (uint)Encoding.UTF8.GetByteCount(name);
        if (name[name.Length - 1] != (char)0)
        {
            // add a trailing nul byte
            nameLength++;
        }
        const uint contentLengthLength = 4;

        uint descriptorLength = imageCountLength + nameLengthLength + nameLength + contentLengthLength;

        return descriptorLength;
    }

    internal static Data PassiveDataSegment(byte[] content)
    {
        return new Data
        {
            Mode = DataMode.Passive,
            Expression = default!,
            MemIdx = 0,
            Content = content
        };
    }

    private Data ComputeDescriptorDataSegment()
    {
        return PassiveDataSegment(ComputeDescriptor());
    }

    private byte[] ComputeDescriptor()
    {
        var buf = new byte[ComputeDescriptorLength()];
        using var ms = new MemoryStream(buf);
        using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            const uint numImages = 1;
            writer.Write(numImages);
            // Image 0 Name
            string name = Images[0].Path;
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            uint nameLength = (uint)nameBytes.Length;
            bool addNul = false;
            if (name[name.Length - 1] != (char)0)
            {
                // need to add a trailing nul byte
                nameLength++;
                addNul = true;
            }
            writer.Write(nameLength);
            writer.Write(nameBytes);
            if (addNul)
                writer.Write((byte)0);

            // Image 0 Content Length
            uint contentLength = (uint)Images[0].Content.Length;
            writer.Write(contentLength);
        }
        ms.Flush();
        return buf;
    }

    private Data ComputeModule0DataSegment()
    {
        using var reader = new BinaryReader(Images[0].Content, Encoding.UTF8, leaveOpen: true);
        return PassiveDataSegment(reader.ReadBytes((int)Images[0].Content.Length));
    }

}