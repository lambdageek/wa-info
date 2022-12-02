using System.Threading;
using System.Threading.Tasks;

using WebAssemblyInfo.WebCIL;

namespace WebAssemblyInfo;

public class TemplateReader : WasmReaderBase
{
    private EmbeddingTemplate _template;

    public TemplateReader(string path) : base(path)
    {
        _template = default!;
    }

    public async Task<EmbeddingTemplate> ReadTemplate(CancellationToken cancellationToken = default)
    {
        _template = new EmbeddingTemplate();
        await System.Threading.Tasks.Task.CompletedTask;
        return _template;
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
        switch (sectionInfo.id)
        {
            case SectionId.Global:
                ReadGlobalSection(sectionInfo);
                break;
        }
    }

    protected void ReadGlobalSection(WasmReaderBase.SectionInfo sectionInfo)
    {
    }
}