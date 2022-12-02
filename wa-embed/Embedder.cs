using System.Threading;
using System.Threading.Tasks;

namespace WebAssemblyInfo;

public class Embedder
{
    public readonly string TemplateModulePath;
    public readonly string OutputModulePath;
    public readonly string AssemblyFilePath;

    public Embedder(string templateModule, string outputModule, string assemblyFile)
    {
        TemplateModulePath = templateModule;
        OutputModulePath = outputModule;
        AssemblyFilePath = assemblyFile;
    }

    public async Task Embed(CancellationToken cancellationToken = default)
    {
        using var templateReader = new TemplateReader(TemplateModulePath);

        var embeddingTemplate = await templateReader.ReadTemplate(cancellationToken);

        await System.Threading.Tasks.Task.CompletedTask;
    }
}