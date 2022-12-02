using System;
using System.IO;
namespace WebAssemblyInfo;

public class WasmWriterUtils
{
    public BinaryWriter Writer { get; }

    public WasmWriterUtils(BinaryWriter writer)
    {
        Writer = writer;
    }

    public void WriteWasmHeader(int version = 1)
    {
        Writer.Write(WasmReaderBase.MagicWasm);
        Writer.Write(version);
    }

    public void WriteU32(uint n)
    {
        do
        {
            byte b = (byte)(n & 0x7f);
            n >>= 7;
            if (n != 0)
                b |= 0x80;
            Writer.Write(b);
        } while (n != 0);
    }

    public static uint U32Len(uint n)
    {
        uint len = 0u;
        do
        {
            n >>= 7;
            len++;
        } while (n != 0);

        return len;
    }

    public void WriteI32(int n)
    {
        var final = false;
        do
        {
            byte b = (byte)(n & 0x7f);
            n >>= 7;

            if ((n == 0 && ((n & 0x80000000) == 0)) || (n == -1 && ((n & 0x80000000) == 0x80)))
                final = true;
            else
                b |= 0x80;

            Writer.Write(b);
        } while (!final);
    }

    public static uint I32Len(int n)
    {
        var final = false;
        var len = 0u;
        do
        {
            n >>= 7;

            if ((n == 0 && ((n & 0x80000000) == 0)) || (n == -1 && ((n & 0x80000000) == 0x80)))
                final = true;

            len++;
        } while (!final);

        return len;
    }


    // i32.const <cn>
    public void WriteConstI32Expr(int cn)
    {
        Writer.Write((byte)Opcode.I32_Const);
        WriteI32(cn);
        Writer.Write((byte)Opcode.End);
    }

    public static uint ConstI32ExprLen(int cn)
    {
        return 2 + I32Len(cn);
    }

    public void WriteDataSegment(DataMode mode, ReadOnlySpan<byte> data, int memoryOffset)
    {
        // data segment
        WriteU32((uint)mode);

        if (mode == DataMode.Active)
            WriteConstI32Expr(memoryOffset);

        WriteU32((uint)data.Length);
        Writer.Write(data);
    }

}