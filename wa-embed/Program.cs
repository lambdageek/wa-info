using System.Threading.Tasks;

namespace WebAssemblyInfo;

public class Program
{
    public static bool Verbose => true;
    public static bool Verbose2 => true;
    public static bool PrintOffsets => false;

    public static void Main(string[] args)
    {

        string inFile = "data/template.wasm";
        string outFile = "out.wasm";
        string asmFile = "/tmp/sample/bin/Debug/net7.0/sample.dll";

        new Embedder(inFile, outFile, asmFile).Embed();
    }
}