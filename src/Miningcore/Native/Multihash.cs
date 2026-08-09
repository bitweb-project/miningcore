using System.Runtime.InteropServices;

namespace Miningcore.Native;

public static unsafe class Multihash
{
    [DllImport("libmultihash", EntryPoint = "sha256dt_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void sha256dt(byte* input, void* output);

    [DllImport("libmultihash", EntryPoint = "sha512_256_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void sha512_256(byte* input, void* output, uint inputLength);

    [DllImport("libmultihash", EntryPoint = "scrypt_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void scrypt(byte* input, void* output, uint n, uint r, uint inputLength);

    [DllImport("libmultihash", EntryPoint = "heavyhash_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void heavyhash(byte* input, void* output, uint inputLength);

    [DllImport("libmultihash", EntryPoint = "yescryptR8_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void yescryptR8(byte* input, void* output, uint inputLength);

    [DllImport("libmultihash", EntryPoint = "yescryptR16_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void yescryptR16(byte* input, void* output, uint inputLength);

    [DllImport("libmultihash", EntryPoint = "yescryptR32_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void yescryptR32(byte* input, void* output, uint inputLength);

    [DllImport("libmultihash", EntryPoint = "yespower_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void yespower(byte* input, void* output, uint inputLength);

    [DllImport("libmultihash", EntryPoint = "yespowerR16_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void yespowerR16(byte* input, void* output, uint inputLength);

    // Argon2id Bitweb: t=3, m=1024, lanes=1, v=0x13
    [DllImport("libmultihash", EntryPoint = "argon2id_bitweb_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void argon2id_bitweb(byte* input, void* output, uint inputLength);

    // generic Argon2: typeId 0=d 1=i 2=id, version 0x10 or 0x13
    [DllImport("libmultihash", EntryPoint = "argon2_generic_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void argon2_generic(byte* input, void* output, uint inputLength,
        uint tCost, uint mCost, uint lanes, uint typeId, uint version);

    // Argon2id Dpowcoin: 2-round, salt=SHA512(SHA512(header))->round1_out, t=2, lanes=2, v=0x13
    // Round1 m=4096 KiB, Round2 m=32768 KiB (params baked into the native export)
    // threads: execution parallelism only, NOT consensus-critical (identical output for any
    // value in [1, lanes]=[1,2]) - configurable per-deployment via coins.json headerHasher.args.
    [DllImport("libmultihash", EntryPoint = "argon2id_dpowcoin_export", CallingConvention = CallingConvention.Cdecl)]
    public static extern void argon2id_dpowcoin(byte* input, void* output, uint inputLength, uint threads);
}
