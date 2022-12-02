using System.IO;

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

    public void Embed()
    {
        using var templateReader = new TemplateReader(TemplateModulePath);

        using var assemblyStream = File.Open(AssemblyFilePath, FileMode.Open);
        using var outputStream = File.Open(OutputModulePath, FileMode.Create);
        using var assemblyReader = new BinaryReader(assemblyStream);
        using var outputWriter = new BinaryWriter(outputStream);

        var templateWriter = new TemplateWriter(outputWriter, assemblyReader, templateReader);

        templateWriter.Write();

        outputWriter.Flush();

    }
}