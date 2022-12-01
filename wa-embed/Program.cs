namespace WebAssemblyInfo;

public class Program
{
    public static void Main(string[] args)
    {
        string inFile = "template.wasm";
        string outFile = "out.wasm";
        string asmFile = "sample.dll";

        new Embedder(inFile, outFile, asmFile).Embed();
    }
}