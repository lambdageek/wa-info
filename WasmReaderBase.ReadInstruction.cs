using System;
using System.IO;

namespace WebAssemblyInfo;

public partial class WasmReaderBase
{
    Instruction ReadInstruction(Opcode opcode)
    {
        Instruction instruction = new() { Offset = Reader.BaseStream.Position - 1 };
        instruction.Opcode = opcode;
        Opcode op;

        // Console.WriteLine($"read opcode: 0x{opcode:x} {opcode}");
        switch (opcode)
        {
            case Opcode.Block:
            case Opcode.Loop:
            case Opcode.If:
            case Opcode.Try:
                // DumpBytes(64);
                instruction.BlockType = ReadBlockType();

                // Console.WriteLine($"blocktype: {instruction.BlockType.Kind}");
                var end = opcode switch
                {
                    Opcode.If => Opcode.Else,
                    Opcode.Try => Opcode.Delegate,
                    _ => Opcode.End,
                };
                (instruction.Block, op) = ReadBlock(end);

                if (op == Opcode.Else)
                    (instruction.Block2, _) = ReadBlock();
                else if (op == Opcode.Delegate)
                {
                    instruction.TryDelegate = true;
                    instruction.Idx = ReadU32();
                }
                break;
            case Opcode.Catch:
            case Opcode.Catch_All:
                // DumpBytes(16);
                if (opcode != Opcode.Catch_All)
                {
                    instruction.I32 = ReadI32();
                    Console.WriteLine($"i32: {instruction.I32}");
                }
                break;
            case Opcode.Throw:
                instruction.I32 = ReadI32();
                break;
            case Opcode.Rethrow:
                instruction.Idx = ReadU32();
                break;
            case Opcode.Memory_Size:
            case Opcode.Memory_Grow:
                op = (Opcode)Reader.ReadByte();
                if (op != Opcode.Unreachable)
                    throw new Exception($"0x00 expected after opcode: {opcode}, got {op} instead");
                break;
            case Opcode.Call:
            case Opcode.Local_Get:
            case Opcode.Local_Set:
            case Opcode.Local_Tee:
            case Opcode.Global_Get:
            case Opcode.Global_Set:
            case Opcode.Br:
            case Opcode.Br_If:
                instruction.Idx = ReadU32();
                break;
            case Opcode.Br_Table:
                var count = ReadU32();

                instruction.IdxArray = new UInt32[count];

                for (var i = 0; i < count; i++)
                    instruction.IdxArray[i] = ReadU32();

                instruction.Idx = ReadU32();

                break;
            case Opcode.Call_Indirect:
                instruction.Idx = ReadU32();
                instruction.Idx2 = ReadU32();
                break;
            case Opcode.I32_Const:
                instruction.I32 = ReadI32();
                break;
            case Opcode.F32_Const:
                instruction.F32 = Reader.ReadSingle();
                break;
            case Opcode.I64_Const:
                instruction.I64 = ReadI64();
                break;
            case Opcode.F64_Const:
                instruction.F64 = Reader.ReadDouble();
                break;
            case Opcode.I32_Load:
            case Opcode.I64_Load:
            case Opcode.F32_Load:
            case Opcode.F64_Load:
            case Opcode.I32_Load8_S:
            case Opcode.I32_Load8_U:
            case Opcode.I32_Load16_S:
            case Opcode.I32_Load16_U:
            case Opcode.I64_Load8_S:
            case Opcode.I64_Load8_U:
            case Opcode.I64_Load16_S:
            case Opcode.I64_Load16_U:
            case Opcode.I64_Load32_S:
            case Opcode.I64_Load32_U:
            case Opcode.I32_Store:
            case Opcode.I64_Store:
            case Opcode.F32_Store:
            case Opcode.F64_Store:
            case Opcode.I32_Store8:
            case Opcode.I32_Store16:
            case Opcode.I64_Store8:
            case Opcode.I64_Store16:
            case Opcode.I64_Store32:
                instruction.MemArg = ReadMemArg();
                break;
            case Opcode.Unreachable:
            case Opcode.Nop:
            case Opcode.Return:
            case Opcode.Drop:
            case Opcode.Select:
            case Opcode.I32_Eqz:
            case Opcode.I32_Eq:
            case Opcode.I32_Ne:
            case Opcode.I32_Lt_S:
            case Opcode.I32_Lt_U:
            case Opcode.I32_Gt_S:
            case Opcode.I32_Gt_U:
            case Opcode.I32_Le_S:
            case Opcode.I32_Le_U:
            case Opcode.I32_Ge_S:
            case Opcode.I32_Ge_U:
            case Opcode.I64_Eqz:
            case Opcode.I64_Eq:
            case Opcode.I64_Ne:
            case Opcode.I64_Lt_S:
            case Opcode.I64_Lt_U:
            case Opcode.I64_Gt_S:
            case Opcode.I64_Gt_U:
            case Opcode.I64_Le_S:
            case Opcode.I64_Le_U:
            case Opcode.I64_Ge_S:
            case Opcode.I64_Ge_U:
            case Opcode.F32_Eq:
            case Opcode.F32_Ne:
            case Opcode.F32_Lt:
            case Opcode.F32_Gt:
            case Opcode.F32_Le:
            case Opcode.F32_Ge:
            case Opcode.F64_Eq:
            case Opcode.F64_Ne:
            case Opcode.F64_Lt:
            case Opcode.F64_Gt:
            case Opcode.F64_Le:
            case Opcode.F64_Ge:
            case Opcode.I32_Clz:
            case Opcode.I32_Ctz:
            case Opcode.I32_Popcnt:
            case Opcode.I32_Add:
            case Opcode.I32_Sub:
            case Opcode.I32_Mul:
            case Opcode.I32_Div_S:
            case Opcode.I32_Div_U:
            case Opcode.I32_Rem_S:
            case Opcode.I32_Rem_U:
            case Opcode.I32_And:
            case Opcode.I32_Or:
            case Opcode.I32_Xor:
            case Opcode.I32_Shl:
            case Opcode.I32_Shr_S:
            case Opcode.I32_Shr_U:
            case Opcode.I32_Rotl:
            case Opcode.I32_Rotr:
            case Opcode.I64_Clz:
            case Opcode.I64_Ctz:
            case Opcode.I64_Popcnt:
            case Opcode.I64_Add:
            case Opcode.I64_Sub:
            case Opcode.I64_Mul:
            case Opcode.I64_Div_S:
            case Opcode.I64_Div_U:
            case Opcode.I64_Rem_S:
            case Opcode.I64_Rem_U:
            case Opcode.I64_And:
            case Opcode.I64_Or:
            case Opcode.I64_Xor:
            case Opcode.I64_Shl:
            case Opcode.I64_Shr_S:
            case Opcode.I64_Shr_U:
            case Opcode.I64_Rotl:
            case Opcode.I64_Rotr:
            case Opcode.F32_Abs:
            case Opcode.F32_Neg:
            case Opcode.F32_Ceil:
            case Opcode.F32_Floor:
            case Opcode.F32_Trunc:
            case Opcode.F32_Nearest:
            case Opcode.F32_Sqrt:
            case Opcode.F32_Add:
            case Opcode.F32_Sub:
            case Opcode.F32_Mul:
            case Opcode.F32_Div:
            case Opcode.F32_Min:
            case Opcode.F32_Max:
            case Opcode.F32_Copysign:
            case Opcode.F64_Abs:
            case Opcode.F64_Neg:
            case Opcode.F64_Ceil:
            case Opcode.F64_Floor:
            case Opcode.F64_Trunc:
            case Opcode.F64_Nearest:
            case Opcode.F64_Sqrt:
            case Opcode.F64_Add:
            case Opcode.F64_Sub:
            case Opcode.F64_Mul:
            case Opcode.F64_Div:
            case Opcode.F64_Min:
            case Opcode.F64_Max:
            case Opcode.F64_Copysign:
            case Opcode.I32_Wrap_I64:
            case Opcode.I32_Trunc_F32_S:
            case Opcode.I32_Trunc_F32_U:
            case Opcode.I32_Trunc_F64_S:
            case Opcode.I32_Trunc_F64_U:
            case Opcode.I64_Extend_I32_S:
            case Opcode.I64_Extend_I32_U:
            case Opcode.I64_Trunc_F32_S:
            case Opcode.I64_Trunc_F32_U:
            case Opcode.I64_Trunc_F64_S:
            case Opcode.I64_Trunc_F64_U:
            case Opcode.F32_Convert_I32_S:
            case Opcode.F32_Convert_I32_U:
            case Opcode.F32_Convert_I64_S:
            case Opcode.F32_Convert_I64_U:
            case Opcode.F32_Demote_F64:
            case Opcode.F64_Convert_I32_S:
            case Opcode.F64_Convert_I32_U:
            case Opcode.F64_Convert_I64_S:
            case Opcode.F64_Convert_I64_U:
            case Opcode.F64_Promote_F32:
            case Opcode.I32_Reinterpret_F32:
            case Opcode.I64_Reinterpret_F64:
            case Opcode.F32_Reinterpret_I32:
            case Opcode.F64_Reinterpret_I64:
            case Opcode.I32_Extend8_S:
            case Opcode.I32_Extend16_S:
            case Opcode.I64_Extend8_S:
            case Opcode.I64_Extend16_S:
            case Opcode.I64_Extend32_S:
                break;

            case Opcode.Prefix:
                ReadPrefixInstruction(ref instruction);
                break;

            case Opcode.SIMDPrefix:
                ReadSIMDInstruction(ref instruction);
                break;

            case Opcode.MTPrefix:
                ReadMTInstruction(ref instruction);
                break;

            default:
                throw new FileLoadException($"Unknown opcode: {opcode} ({opcode:x})");
        }

        return instruction;
    }

    void ReadPrefixInstruction(ref Instruction instruction)
    {
        instruction.PrefixOpcode = (PrefixOpcode)ReadU32();
        // Console.WriteLine($"Prefix opcode: {instruction.PrefixOpcode}");
        switch (instruction.PrefixOpcode)
        {
            case PrefixOpcode.I32_Trunc_Sat_F32_S:
            case PrefixOpcode.I32_Trunc_Sat_F32_U:
            case PrefixOpcode.I32_Trunc_Sat_F64_S:
            case PrefixOpcode.I32_Trunc_Sat_F64_U:
            case PrefixOpcode.I64_Trunc_Sat_F32_S:
            case PrefixOpcode.I64_Trunc_Sat_F32_U:
            case PrefixOpcode.I64_Trunc_Sat_F64_S:
            case PrefixOpcode.I64_Trunc_Sat_F64_U:
                break;
            case PrefixOpcode.Memory_Init:
                instruction.Idx = ReadU32();
                Reader.ReadByte();
                break;
            case PrefixOpcode.Data_Drop:
                instruction.Idx = ReadU32();
                break;
            case PrefixOpcode.Memory_Copy:
                Reader.ReadByte();
                Reader.ReadByte();
                break;
            case PrefixOpcode.Memory_Fill:
                Reader.ReadByte();
                break;
            case PrefixOpcode.Table_Init:
            case PrefixOpcode.Table_Copy:
                instruction.Idx = ReadU32();
                instruction.Idx2 = ReadU32();
                break;
            case PrefixOpcode.Elem_Drop:
            case PrefixOpcode.Table_Grow:
            case PrefixOpcode.Table_Size:
            case PrefixOpcode.Table_Fill:
                instruction.Idx = ReadU32();
                break;
            default:
                throw new FileLoadException($"Unknown Prefix opcode: {instruction.PrefixOpcode} ({instruction.PrefixOpcode:x})");
        }
    }

    void ReadSIMDInstruction(ref Instruction instruction)
    {
        instruction.SIMDOpcode = (SIMDOpcode)ReadU32();
        //Console.WriteLine($"SIMD opcode: {instruction.SIMDOpcode}");

        switch (instruction.SIMDOpcode)
        {
            case SIMDOpcode.V128_Load:
            case SIMDOpcode.V128_Load8x8_S:
            case SIMDOpcode.V128_Load8x8_U:
            case SIMDOpcode.V128_Load16x4_S:
            case SIMDOpcode.V128_Load16x4_U:
            case SIMDOpcode.V128_Load32x2_S:
            case SIMDOpcode.V128_Load32x2_U:
            case SIMDOpcode.V128_Load8_Splat:
            case SIMDOpcode.V128_Load16_Splat:
            case SIMDOpcode.V128_Load32_Splat:
            case SIMDOpcode.V128_Load64_Splat:
            case SIMDOpcode.V128_Store:
            case SIMDOpcode.V128_Load32_Zero:
            case SIMDOpcode.V128_Load64_Zero:
                instruction.MemArg = ReadMemArg();
                break;
            case SIMDOpcode.V128_Load8_Lane:
            case SIMDOpcode.V128_Load16_Lane:
            case SIMDOpcode.V128_Load32_Lane:
            case SIMDOpcode.V128_Load64_Lane:
            case SIMDOpcode.V128_Store8_Lane:
            case SIMDOpcode.V128_Store16_Lane:
            case SIMDOpcode.V128_Store32_Lane:
            case SIMDOpcode.V128_Store64_Lane:
                instruction.MemArg = ReadMemArg();
                instruction.SIMDImmLaneIdx = Reader.ReadByte();
                break;
            case SIMDOpcode.V128_Const:
                instruction.SIMDImmByteArray = Reader.ReadBytes(16);
                break;
            case SIMDOpcode.I8x16_Shuffle:
                instruction.SIMDImmByteArray = Reader.ReadBytes(16);
                break;
            case SIMDOpcode.I8x16_Extract_Lane_S:
            case SIMDOpcode.I8x16_Extract_Lane_U:
            case SIMDOpcode.I8x16_Replace_Lane:
            case SIMDOpcode.I16x8_Extract_Lane_S:
            case SIMDOpcode.I16x8_Extract_Lane_U:
            case SIMDOpcode.I16x8_Replace_Lane:
            case SIMDOpcode.I32x4_Extract_Lane:
            case SIMDOpcode.I32x4_Replace_Lane:
            case SIMDOpcode.I64x2_Extract_Lane:
            case SIMDOpcode.I64x2_Replace_Lane:
            case SIMDOpcode.F32x4_Extract_Lane:
            case SIMDOpcode.F32x4_Replace_Lane:
            case SIMDOpcode.F64x2_Extract_Lane:
            case SIMDOpcode.F64x2_Replace_Lane:
                instruction.SIMDImmLaneIdx = Reader.ReadByte();
                break;
            case SIMDOpcode.I8x16_Swizzle:
            case SIMDOpcode.I8x16_Splat:
            case SIMDOpcode.I16x8_Splat:
            case SIMDOpcode.I32x4_Splat:
            case SIMDOpcode.I64x2_Splat:
            case SIMDOpcode.F32x4_Splat:
            case SIMDOpcode.F64x2_Splat:
            case SIMDOpcode.I8x16_Eq:
            case SIMDOpcode.I8x16_Ne:
            case SIMDOpcode.I8x16_Lt_S:
            case SIMDOpcode.I8x16_Lt_U:
            case SIMDOpcode.I8x16_Gt_S:
            case SIMDOpcode.I8x16_Gt_U:
            case SIMDOpcode.I8x16_Le_S:
            case SIMDOpcode.I8x16_Le_U:
            case SIMDOpcode.I8x16_Ge_S:
            case SIMDOpcode.I8x16_Ge_U:
            case SIMDOpcode.I16x8_Eq:
            case SIMDOpcode.I16x8_Ne:
            case SIMDOpcode.I16x8_Lt_S:
            case SIMDOpcode.I16x8_Lt_U:
            case SIMDOpcode.I16x8_Gt_S:
            case SIMDOpcode.I16x8_Gt_U:
            case SIMDOpcode.I16x8_Le_S:
            case SIMDOpcode.I16x8_Le_U:
            case SIMDOpcode.I16x8_Ge_S:
            case SIMDOpcode.I16x8_Ge_U:
            case SIMDOpcode.I32x4_Eq:
            case SIMDOpcode.I32x4_Ne:
            case SIMDOpcode.I32x4_Lt_S:
            case SIMDOpcode.I32x4_Lt_U:
            case SIMDOpcode.I32x4_Gt_S:
            case SIMDOpcode.I32x4_Gt_U:
            case SIMDOpcode.I32x4_Le_S:
            case SIMDOpcode.I32x4_Le_U:
            case SIMDOpcode.I32x4_Ge_S:
            case SIMDOpcode.I32x4_Ge_U:
            case SIMDOpcode.F32x4_Eq:
            case SIMDOpcode.F32x4_Ne:
            case SIMDOpcode.F32x4_Lt:
            case SIMDOpcode.F32x4_Gt:
            case SIMDOpcode.F32x4_Le:
            case SIMDOpcode.F32x4_Ge:
            case SIMDOpcode.F64x2_Eq:
            case SIMDOpcode.F64x2_Ne:
            case SIMDOpcode.F64x2_Lt:
            case SIMDOpcode.F64x2_Gt:
            case SIMDOpcode.F64x2_Le:
            case SIMDOpcode.F64x2_Ge:
            case SIMDOpcode.V128_Not:
            case SIMDOpcode.V128_And:
            case SIMDOpcode.V128_Andnot:
            case SIMDOpcode.V128_Or:
            case SIMDOpcode.V128_Xor:
            case SIMDOpcode.V128_Bitselect:
            case SIMDOpcode.I8x16_Abs:
            case SIMDOpcode.I8x16_Neg:
            case SIMDOpcode.I8x16_All_True:
            case SIMDOpcode.I8x16_Bitmask:
            case SIMDOpcode.I8x16_Narrow_I16x8_S:
            case SIMDOpcode.I8x16_Narrow_I16x8_U:
            case SIMDOpcode.I8x16_Shl:
            case SIMDOpcode.I8x16_Shr_S:
            case SIMDOpcode.I8x16_Shr_U:
            case SIMDOpcode.I8x16_Add:
            case SIMDOpcode.I8x16_Add_Sat_S:
            case SIMDOpcode.I8x16_Add_Sat_U:
            case SIMDOpcode.I8x16_Sub:
            case SIMDOpcode.I8x16_Sub_Sat_S:
            case SIMDOpcode.I8x16_Sub_Sat_U:
            case SIMDOpcode.I8x16_Min_S:
            case SIMDOpcode.I8x16_Min_U:
            case SIMDOpcode.I8x16_Max_S:
            case SIMDOpcode.I8x16_Max_U:
            case SIMDOpcode.I8x16_Avgr_U:
            case SIMDOpcode.I16x8_Abs:
            case SIMDOpcode.I16x8_Neg:
            case SIMDOpcode.I16x8_All_True:
            case SIMDOpcode.I16x8_Bitmask:
            case SIMDOpcode.I16x8_Narrow_I32x4_S:
            case SIMDOpcode.I16x8_Narrow_I32x4_U:
            case SIMDOpcode.I16x8_Extend_Low_I8x16_S:
            case SIMDOpcode.I16x8_Extend_High_I8x16_S:
            case SIMDOpcode.I16x8_Extend_Low_I8x16_U:
            case SIMDOpcode.I16x8_Extend_High_I8x16_U:
            case SIMDOpcode.I16x8_Shl:
            case SIMDOpcode.I16x8_Shr_S:
            case SIMDOpcode.I16x8_Shr_U:
            case SIMDOpcode.I16x8_Add:
            case SIMDOpcode.I16x8_Add_Sat_S:
            case SIMDOpcode.I16x8_Add_Sat_U:
            case SIMDOpcode.I16x8_Sub:
            case SIMDOpcode.I16x8_Sub_Sat_S:
            case SIMDOpcode.I16x8_Sub_Sat_U:
            case SIMDOpcode.I16x8_Mul:
            case SIMDOpcode.I16x8_Min_S:
            case SIMDOpcode.I16x8_Min_U:
            case SIMDOpcode.I16x8_Max_S:
            case SIMDOpcode.I16x8_Max_U:
            case SIMDOpcode.I16x8_Avgr_U:
            case SIMDOpcode.I32x4_Abs:
            case SIMDOpcode.I32x4_Neg:
            case SIMDOpcode.I32x4_All_True:
            case SIMDOpcode.I32x4_Bitmask:
            case SIMDOpcode.I32x4_Extend_Low_I16x8_S:
            case SIMDOpcode.I32x4_Extend_High_I16x8_S:
            case SIMDOpcode.I32x4_Extend_Low_I16x8_U:
            case SIMDOpcode.I32x4_Extend_High_I16x8_U:
            case SIMDOpcode.I32x4_Shl:
            case SIMDOpcode.I32x4_Shr_S:
            case SIMDOpcode.I32x4_Shr_U:
            case SIMDOpcode.I32x4_Add:
            case SIMDOpcode.I32x4_Sub:
            case SIMDOpcode.I32x4_Mul:
            case SIMDOpcode.I32x4_Min_S:
            case SIMDOpcode.I32x4_Min_U:
            case SIMDOpcode.I32x4_Max_S:
            case SIMDOpcode.I32x4_Max_U:
            case SIMDOpcode.I32x4_Dot_I16x8_S:
            case SIMDOpcode.I64x2_Abs:
            case SIMDOpcode.I64x2_Neg:
            case SIMDOpcode.I64x2_Bitmask:
            case SIMDOpcode.I64x2_Extend_Low_I32x4_S:
            case SIMDOpcode.I64x2_Extend_High_I32x4_S:
            case SIMDOpcode.I64x2_Extend_Low_I32x4_U:
            case SIMDOpcode.I64x2_Extend_High_I32x4_U:
            case SIMDOpcode.I64x2_Shl:
            case SIMDOpcode.I64x2_Shr_S:
            case SIMDOpcode.I64x2_Shr_U:
            case SIMDOpcode.I64x2_Add:
            case SIMDOpcode.I64x2_Sub:
            case SIMDOpcode.I64x2_Mul:
            case SIMDOpcode.F32x4_Ceil:
            case SIMDOpcode.F32x4_Floor:
            case SIMDOpcode.F32x4_Trunc:
            case SIMDOpcode.F32x4_Nearest:
            case SIMDOpcode.F64x2_Ceil:
            case SIMDOpcode.F64x2_Floor:
            case SIMDOpcode.F64x2_Trunc:
            case SIMDOpcode.F64x2_Nearest:
            case SIMDOpcode.F32x4_Abs:
            case SIMDOpcode.F32x4_Neg:
            case SIMDOpcode.F32x4_Sqrt:
            case SIMDOpcode.F32x4_Add:
            case SIMDOpcode.F32x4_Sub:
            case SIMDOpcode.F32x4_Mul:
            case SIMDOpcode.F32x4_Div:
            case SIMDOpcode.F32x4_Min:
            case SIMDOpcode.F32x4_Max:
            case SIMDOpcode.F32x4_Pmin:
            case SIMDOpcode.F32x4_Pmax:
            case SIMDOpcode.F64x2_Abs:
            case SIMDOpcode.F64x2_Neg:
            case SIMDOpcode.F64x2_Sqrt:
            case SIMDOpcode.F64x2_Add:
            case SIMDOpcode.F64x2_Sub:
            case SIMDOpcode.F64x2_Mul:
            case SIMDOpcode.F64x2_Div:
            case SIMDOpcode.F64x2_Min:
            case SIMDOpcode.F64x2_Max:
            case SIMDOpcode.F64x2_Pmin:
            case SIMDOpcode.F64x2_Pmax:
            case SIMDOpcode.I32x4_Trunc_Sat_F32x4_S:
            case SIMDOpcode.I32x4_Trunc_Sat_F32x4_U:
            case SIMDOpcode.F32x4_Convert_I32x4_S:
            case SIMDOpcode.F32x4_Convert_I32x4_U:
            case SIMDOpcode.I16x8_Extmul_Low_I8x16_S:
            case SIMDOpcode.I16x8_Extmul_High_I8x16_S:
            case SIMDOpcode.I16x8_Extmul_Low_I8x16_U:
            case SIMDOpcode.I16x8_Extmul_High_I8x16_U:
            case SIMDOpcode.I32x4_Extmul_Low_I16x8_S:
            case SIMDOpcode.I32x4_Extmul_High_I16x8_S:
            case SIMDOpcode.I32x4_Extmul_Low_I16x8_U:
            case SIMDOpcode.I32x4_Extmul_High_I16x8_U:
            case SIMDOpcode.I64x2_Extmul_Low_I32x4_S:
            case SIMDOpcode.I64x2_Extmul_High_I32x4_S:
            case SIMDOpcode.I64x2_Extmul_Low_I32x4_U:
            case SIMDOpcode.I64x2_Extmul_High_I32x4_U:
            case SIMDOpcode.I16x8_Q15mulr_Sat_S:
            case SIMDOpcode.V128_Any_True:
            case SIMDOpcode.I64x2_Eq:
            case SIMDOpcode.I64x2_Ne:
            case SIMDOpcode.I64x2_Lt_S:
            case SIMDOpcode.I64x2_Gt_S:
            case SIMDOpcode.I64x2_Le_S:
            case SIMDOpcode.I64x2_Ge_S:
            case SIMDOpcode.I64x2_All_True:
            case SIMDOpcode.F64x2_Convert_Low_I32x4_S:
            case SIMDOpcode.F64x2_Convert_Low_I32x4_U:
            case SIMDOpcode.I32x4_Trunc_Sat_F64x2_S_Zero:
            case SIMDOpcode.I32x4_Trunc_Sat_F64x2_U_Zero:
            case SIMDOpcode.F32x4_Demote_F64x2_Zero:
            case SIMDOpcode.F64x2_Promote_Low_F32x4:
            case SIMDOpcode.I8x16_Popcnt:
            case SIMDOpcode.I16x8_Extadd_Pairwise_I8x16_S:
            case SIMDOpcode.I16x8_Extadd_Pairwise_I8x16_U:
            case SIMDOpcode.I32x4_Extadd_Pairwise_I16x8_S:
            case SIMDOpcode.I32x4_Extadd_Pairwise_I16x8_U:
                break;
            default:
                throw new FileLoadException($"Unknown SIMD opcode: {instruction.SIMDOpcode} ({instruction.SIMDOpcode:x})");
        }
    }

    void ReadMTInstruction(ref Instruction instruction)
    {
        instruction.MTOpcode = (MTOpcode)ReadU32();
        // Console.WriteLine($"MT opcode: {instruction.MTOpcode}");

        switch (instruction.MTOpcode)
        {
            case MTOpcode.Atomic_Fence:
                Reader.ReadByte();
                break;
            case MTOpcode.Memory_Atomic_Notify:
            case MTOpcode.Memory_Atomic_Wait32:
            case MTOpcode.Memory_Atomic_Wait64:
            case MTOpcode.I32_Atomic_Load:
            case MTOpcode.I64_Atomic_Load:
            case MTOpcode.I32_Atomic_Load8_u:
            case MTOpcode.I32_Atomic_Load16_u:
            case MTOpcode.I64_Atomic_Load8_u:
            case MTOpcode.I64_Atomic_Load16_u:
            case MTOpcode.I64_Atomic_Load32_u:
            case MTOpcode.I32_Atomic_Store:
            case MTOpcode.I64_Atomic_Store:
            case MTOpcode.I32_Atomic_Store8:
            case MTOpcode.I32_Atomic_Store16:
            case MTOpcode.I64_Atomic_Store8:
            case MTOpcode.I64_Atomic_Store16:
            case MTOpcode.I64_Atomic_Store32:
            case MTOpcode.I32_Atomic_Rmw_Add:
            case MTOpcode.I64_Atomic_Rmw_Add:
            case MTOpcode.I32_Atomic_Rmw8_Add_U:
            case MTOpcode.I32_Atomic_Rmw16_Add_U:
            case MTOpcode.I64_Atomic_Rmw8_Add_U:
            case MTOpcode.I64_Atomic_Rmw16_Add_U:
            case MTOpcode.I64_Atomic_Rmw32_Add_U:
            case MTOpcode.I32_Atomic_Rmw_Sub:
            case MTOpcode.I64_Atomic_Rmw_Sub:
            case MTOpcode.I32_Atomic_Rmw8_Sub_U:
            case MTOpcode.I32_Atomic_Rmw16_Sub_U:
            case MTOpcode.I64_Atomic_Rmw8_Sub_U:
            case MTOpcode.I64_Atomic_Rmw16_Sub_U:
            case MTOpcode.I64_Atomic_Rmw32_Sub_U:
            case MTOpcode.I32_Atomic_Rmw_And:
            case MTOpcode.I64_Atomic_Rmw_And:
            case MTOpcode.I32_Atomic_Rmw8_And_U:
            case MTOpcode.I32_Atomic_Rmw16_And_U:
            case MTOpcode.I64_Atomic_Rmw8_And_U:
            case MTOpcode.I64_Atomic_Rmw16_And_U:
            case MTOpcode.I64_Atomic_Rmw32_And_U:
            case MTOpcode.I32_Atomic_Rmw_Or:
            case MTOpcode.I64_Atomic_Rmw_Or:
            case MTOpcode.I32_Atomic_Rmw8_Or_U:
            case MTOpcode.I32_Atomic_Rmw16_Or_U:
            case MTOpcode.I64_Atomic_Rmw8_Or_U:
            case MTOpcode.I64_Atomic_Rmw16_Or_U:
            case MTOpcode.I64_Atomic_Rmw32_Or_U:
            case MTOpcode.I32_Atomic_Rmw_Xor:
            case MTOpcode.I64_Atomic_Rmw_Xor:
            case MTOpcode.I32_Atomic_Rmw8_Xor_U:
            case MTOpcode.I32_Atomic_Rmw16_Xor_U:
            case MTOpcode.I64_Atomic_Rmw8_Xor_U:
            case MTOpcode.I64_Atomic_Rmw16_Xor_U:
            case MTOpcode.I64_Atomic_Rmw32_Xor_U:
            case MTOpcode.I32_Atomic_Rmw_Xchg:
            case MTOpcode.I64_Atomic_Rmw_Xchg:
            case MTOpcode.I32_Atomic_Rmw8_Xchg_U:
            case MTOpcode.I32_Atomic_Rmw16_Xchg_U:
            case MTOpcode.I64_Atomic_Rmw8_Xchg_U:
            case MTOpcode.I64_Atomic_Rmw16_Xchg_U:
            case MTOpcode.I64_Atomic_Rmw32_Xchg_U:
            case MTOpcode.I32_Atomic_Rmw_CmpXchg:
            case MTOpcode.I64_Atomic_Rmw_CmpXchg:
            case MTOpcode.I32_Atomic_Rmw8_CmpXchg_U:
            case MTOpcode.I32_Atomic_Rmw16_CmpXchg_U:
            case MTOpcode.I64_Atomic_Rmw8_CmpXchg_U:
            case MTOpcode.I64_Atomic_Rmw16_CmpXchg_U:
            case MTOpcode.I64_Atomic_Rmw32_CmpXchg_U:
                instruction.MemArg = ReadMemArg();
                break;
            default:
                throw new FileLoadException($"Unknown MT opcode: {instruction.MTOpcode} ({instruction.MTOpcode:x})");
        }
    }

}