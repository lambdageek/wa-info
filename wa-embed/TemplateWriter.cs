using System;
using System.IO;

using WebAssemblyInfo.WebCIL;

namespace WebAssemblyInfo;

public sealed class TemplateWriter
{
    private readonly string _outputModulePath;
    private readonly BinaryReader _assemblyReader;
    private readonly TemplateReader _templateReader;

    private readonly BinaryWriter _writer;

    private readonly WasmWriterUtils WU;

    TemplateReader Reader => _templateReader;

    EmbeddingTemplate Template => Reader.Template;

    public TemplateWriter(BinaryWriter writer, BinaryReader assemblyReader, TemplateReader templateReader)
    {
        _assemblyReader = assemblyReader;
        _templateReader = templateReader;
        _writer = writer;
        WU = new WasmWriterUtils(writer);
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
            // FIXME: fixup DataCount section?
        }
    }

    private void WriteGlobalSection()
    {
        var globals = Reader.Globals;

        using var sectionStream = new MemoryStream();

        using (var sectionWriter = new BinaryWriter(sectionStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            var su = new WasmWriterUtils(sectionWriter);
            su.WriteU32((uint)globals.Count);
            foreach ((var global, var idx) in globals.WithIndex())
            {
                // TODO: replace globals with known indexes with our own values
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
        var segments = Reader.DataSegments;

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
}