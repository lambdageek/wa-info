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

    internal void WriteDataSegment(DataMode mode, ReadOnlySpan<byte> data, ReadOnlySpan<Instruction> memoryOffset)
    {
        // data segment
        WriteU32((uint)mode);

        switch (mode)
        {
            case DataMode.Active:
                WriteBlock(memoryOffset);
                break;
            case DataMode.ActiveMemory:
                throw new NotImplementedException("TODO: implement writing ActiveMemory data segments");
            case DataMode.Passive:
                break;
        }

        WriteU32((uint)data.Length);
        Writer.Write(data);
    }

    public void WriteDataSegment(DataMode mode, ReadOnlySpan<byte> data, int memoryOffset)
    {
        WriteDataSegment(mode, data, I32ConstExpr(memoryOffset));
    }

    private static Instruction[] I32ConstExpr(int n)
    {
        return new Instruction[] { new Instruction { Opcode = Opcode.I32_Const, I32 = n } };
    }

    internal void WriteDataSegment(Data segment)
    {
        WriteDataSegment(segment.Mode, segment.Content, segment.Expression);

    }

    public void WriteSectionHeader(WasmReaderBase.SectionId id, uint size)
    {
        Writer.Write((byte)id);
        WriteU32(size);
    }

    internal void WriteBlock(ReadOnlySpan<Instruction> body)
    {
        if (body.Length != 1)
            throw new NotImplementedException("TODO: implement WriteBlock for more than 1 instruction");

        WriteInstruction(body[0]);
        WriteEnd();
    }

    internal void WriteInstruction(Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case Opcode.I32_Const:
                Writer.Write((byte)instruction.Opcode);
                WriteI32((int)instruction.I32);
                break;
            default:
                throw new NotImplementedException($"TODO: implement WriteInstruction for {instruction}");
        }
    }

    internal void WriteEnd()
    {
        Writer.Write((byte)Opcode.End);
    }

    internal void WriteGlobal(Global global)
    {
        Writer.Write(global.Type.value);
        Writer.Write((byte)global.Mutability);
        WriteBlock(global.Expression);
    }


}