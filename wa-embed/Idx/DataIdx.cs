namespace WebAssemblyInfo.Idx;

public record struct DataIdx(int Index)
{
    public static implicit operator DataIdx(int index) => new DataIdx(index);
}
