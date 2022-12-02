using System;
using System.IO;

namespace WebAssemblyInfo;

public sealed class TemplateWriter
{
    private readonly string _outputModulePath;
    private readonly BinaryReader _assemblyReader;
    private readonly TemplateReader _templateReader;

    private readonly BinaryWriter _writer;

    private readonly WasmWriterUtils WU;

    TemplateReader Reader => _templateReader;

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
                    WriteSection(section);
                    break;
            }
            // FIXME: fixup DataCount section?
        }
    }

    private void WriteGlobalSection()
    {
        throw new NotImplementedException();
    }

    private void WriteDataSection()
    {
        throw new NotImplementedException();
    }

    private void WriteSection(WasmReaderBase.SectionInfo section)
    {
        Reader.Reader.BaseStream.Seek(section.offset, SeekOrigin.Begin);
        _writer.Write(Reader.Reader.ReadBytes((int)section.size + (int)(section.begin - section.offset)));
    }
}