namespace WebAssemblyInfo.Idx;

public record struct GlobalIdx(int Index)
{
    public static implicit operator GlobalIdx(int index) => new GlobalIdx(index);
}
