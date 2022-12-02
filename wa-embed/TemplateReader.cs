using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using WebAssemblyInfo.WebCIL;

namespace WebAssemblyInfo;

public class TemplateReader : WasmReaderBase
{
    private EmbeddingTemplate _template;

    private IReadOnlyCollection<Global> _globals;
    private IReadOnlyCollection<Export> _exports;

    private List<SectionInfo> _sections;

    private IReadOnlyCollection<Data> _dataSegments;

    IReadOnlyCollection<Global> Globals => _globals;

    IReadOnlyCollection<Export> Exports => _exports;

    IReadOnlyCollection<Data> DataSegments => _dataSegments;

    internal IReadOnlyCollection<SectionInfo> Sections => _sections;

    public TemplateReader(string path) : base(path)
    {
        _template = default!;
        _globals = default!;
        _exports = default!;
        _sections = default!;
    }

    public void ReadTemplate()
    {
        _template = new EmbeddingTemplate();
        _sections = new();
        ReadModule();
        FindKnownGlobals();
        FindExpectedData();
        return;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _template = default!;
        }
        base.Dispose(disposing);
    }

    protected override void ReadSection(WasmReaderBase.SectionInfo sectionInfo)
    {
        _sections.Add(sectionInfo);
        switch (sectionInfo.id)
        {
            case SectionId.Global:
                ReadGlobalSection();
                break;
            case SectionId.Export:
                ReadExportSection();
                break;
            case SectionId.Data:
                ReadDataSection();
                break;
        }
    }

    protected void ReadGlobalSection()
    {
        var count = ReadU32();
        if (Program.Verbose2)
            Console.WriteLine();

        var globals = new Global[count];
        for (int i = 0; i < count; i++)
        {
            if (Program.Verbose2)
                Console.Write($"  global idx: {i}");

            ReadGlobal(ref globals[i]);

            if (Program.Verbose2)
                Console.WriteLine();
        }
        _globals = globals;
    }

    protected void ReadExportSection()
    {
        var count = ReadU32();
        if (Program.Verbose2)
            Console.WriteLine();

        var exports = new Export[count];
        for (int i = 0; i < count; i++)
        {
            ReadExport(ref exports[i]);
        }
        _exports = exports;
    }

    protected void ReadDataSection()
    {
        var count = ReadU32();

        if (Program.Verbose2)
            Console.WriteLine();

        var dataSegments = new Data[count];
        for (uint i = 0; i < count; i++)
        {
            ReadDataSegment(ref dataSegments[i]);
        }
        _dataSegments = dataSegments;

    }

    static void AssertExportDesc(Export export, ExportDesc desc)
    {
        if (export.Desc != desc)
            throw new InvalidOperationException("Expected global export {export.Name}");
    }
    protected void FindKnownGlobals()
    {
        var t = _template;
        var sawGetDescriptor = false;
        var sawGetModuleData = false;
        foreach (var export in Exports)
        {
            switch (export.Name)
            {
                case Constants.MonoWebCilVersion:
                    AssertExportDesc(export, ExportDesc.GlobalIdx);
                    t = t with { VersionGlobalIdx = export.Idx };
                    break;
                case Constants.MonoWebCilDescriptorLength:
                    AssertExportDesc(export, ExportDesc.GlobalIdx);
                    t = t with { DescriptorLengthGlobalIdx = export.Idx };
                    break;
                case Constants.MonoWebCilGetDescriptor:
                    AssertExportDesc(export, ExportDesc.FuncIdx);
                    sawGetDescriptor = true;
                    break;
                case Constants.MonoWebCilGetModuleData:
                    AssertExportDesc(export, ExportDesc.FuncIdx);
                    sawGetModuleData = true;
                    break;
            }
        }
        if (!sawGetDescriptor)
            throw new InvalidOperationException("Expected export MonoWebCilGetDescriptor");
        if (!sawGetModuleData)
            throw new InvalidOperationException("Expected export MonoWebCilGetModuleData");
        _template = t;
    }

    protected void FindExpectedData()
    {
        foreach ((var dataSegment, var idx) in DataSegments.WithIndex())
        {
            switch (idx)
            {
                case 0:
                    if (dataSegment.Mode != DataMode.Passive)
                        throw new InvalidOperationException($"Expected passive data segment {idx}");
                    _template = _template with { DescriptorDataIdx = 0 };
                    break;
                case 1:
                    if (dataSegment.Mode != DataMode.Passive)
                        throw new InvalidOperationException($"Expected passive data segment {idx}");
                    _template = _template with { Module0DataIdx = 1 };
                    break;
            }
        }
    }
}