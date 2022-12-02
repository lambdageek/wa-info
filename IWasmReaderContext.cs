using System;
namespace WebAssemblyInfo;

public interface IWasmReaderContext
{
    string GlobalName(UInt32 idx);
    string FunctionName(UInt32 idx);
    string FunctionType(UInt32 idx);

    string GetFunctionName(UInt32 idx, bool needsOffset);
}