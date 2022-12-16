using System.Runtime.InteropServices;
namespace ProjectOrigin.PedersenCommitment.Ristretto;

public record RangeProof
{
    internal class Native
    {
        [DllImport("rust_ffi", EntryPoint = "rangeproof_prove_single")]
        internal static extern RangeProofWithCommit ProveSingle(IntPtr bp_gen, IntPtr pc_gen, ulong v, IntPtr blinding, uint n, byte[] label, int label_len);

        [DllImport("rust_ffi", EntryPoint = "rangeproof_prove_multiple")]
        internal static extern RangeProofWithCommit ProveMultiple(IntPtr bp_gen, IntPtr pc_gen, ulong[] v, IntPtr blinding, uint n, byte[] label, int label_len, int amount);

        [DllImport("rust_ffi", EntryPoint = "rangeproof_verify_single")]
        internal static extern bool VerifySingle(IntPtr self, IntPtr bp_gen, IntPtr pc_gen, IntPtr commit, uint n, byte[] label, int label_len);

        [DllImport("rust_ffi", EntryPoint = "rangeproof_verify_multiple")]
        internal static extern bool VerifyMultiple(IntPtr self, IntPtr bp_gen, IntPtr pc_gen, IntPtr commits, uint n, byte[] label, int label_len);

        [DllImport("rust_ffi", EntryPoint = "rangeproof_free")]
        internal static extern void Free(IntPtr self);

    }

    private readonly IntPtr _ptr;

    private RangeProof(IntPtr ptr)
    {
        _ptr = ptr;
    }

    ~RangeProof()
    {
        Native.Free(_ptr);
    }

    public static (RangeProof proof, CompressedPoint commitment) ProveSingle
        (
            BulletProofGen bp_gen,
            Generator pc_gen,
            ulong v,
            Scalar blinding,
            uint n,
            byte[] label
        )
    {
        var tuple = Native.ProveSingle(
                bp_gen._ptr,
                pc_gen._ptr,
                v,
                blinding._ptr,
                n,
                label,
                label.Length
                );

        var bytes = new byte[32];
        CompressedPoint.ToBytes(tuple.CompressedPoint, bytes);
        return (new RangeProof(tuple.Proof), new CompressedPoint(bytes));
    }

    public bool VerifySingle
        (
            BulletProofGen bp_gen,
            Generator pc_gen,
            CompressedPoint commitment, // Should be a CompressedPoint
            uint n,
            byte[] label
        )
    {
        var commit_ptr = CompressedPoint.FromBytes(commitment._bytes);
        var res = Native.VerifySingle(
                _ptr,
                bp_gen._ptr,
                pc_gen._ptr,
                commit_ptr,
                n,
                label,
                label.Length
                );
        return res;
    }
}

[StructLayout(LayoutKind.Sequential)]
struct RangeProofWithCommit
{
    public IntPtr Proof;
    public IntPtr CompressedPoint;
}

public record BulletProofGen
{
    public static Lazy<BulletProofGen> LazyGenerator = new Lazy<BulletProofGen>(() =>
    {
        return new BulletProofGen(32, 1);
    }, true);

    public static BulletProofGen Default
    {
        get => LazyGenerator.Value;
    }

    internal readonly IntPtr _ptr;

    [DllImport("rust_ffi", EntryPoint = "bpgen_new")]
    private static extern IntPtr New(uint gensCapacity, uint partyCapacity);

    [DllImport("rust_ffi", EntryPoint = "bpgen_free")]
    private static extern void Free(IntPtr self);

    public BulletProofGen(uint gensCapacity, uint partyCapacity)
    {
        _ptr = New(gensCapacity, partyCapacity);
    }

    ~BulletProofGen()
    {
        Free(_ptr);
    }

}

