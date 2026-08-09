/*
 * argon2_dpowcoin.cpp
 *
 * Pool-facing Argon2 export for Dpowcoin's two-round Argon2id PoW.
 * Reuses the existing Bitweb-optimised Argon2 backend (argon2bitweb/) —
 * same algorithm, only the parameters and salt-chaining differ from
 * argon2id_bitweb_export.
 *
 * Matches CBlockHeader::GetArgon2idPoWHash() in dpowcoin/src/primitives/block.cpp:
 *
 *   salt          = SHA-512( SHA-512( header ) )                [64 bytes]
 *
 *   Round 1: pwd = header (80 bytes), salt = salt (64 bytes)
 *            t_cost=2, m_cost=4096 KiB, lanes=2, version=0x13
 *            -> round1_out [32 bytes]
 *
 *   Round 2: pwd = header (80 bytes), salt = round1_out (32 bytes)
 *            t_cost=2, m_cost=32768 KiB, lanes=2, version=0x13
 *            -> final hash [32 bytes]
 *
 * SHA-512 here is the full (64-byte, standard IV) SHA-512 — NOT the
 * sha512_256.c already in this directory, which is the truncated
 * SHA-512/256 variant with different initial hash values and is not
 * usable for this. See sha512_dpowcoin.cpp/.h: a standalone CSHA512
 * adapted from dpowcoin/src/crypto/sha512.cpp (the node's own consensus
 * implementation), renamed to CSHA512Dpowcoin to avoid any symbol clash
 * with sha512_256.c in this same library. Kept in C++ (not the cpuminer-opt
 * C version) so this whole export stays a single language, no C/C++ mixing.
 *
 * Exported symbol (extern "C", visible in libmultihash.so):
 *
 *   argon2id_dpowcoin_export
 *       pwd = 80-byte serialised block header, output = 32 bytes.
 *       threads: execution parallelism ONLY, not consensus-critical (see
 *       note above ctx.threads below). Exposed as a parameter so it can be
 *       tuned per-deployment from coins.json (headerHasher.args), the same
 *       way Scrypt's (n, r) are - no native recompile needed to change it.
 *       The node itself always uses threads=1; any value >=1 here produces
 *       an identical hash, only the wall-clock time to compute it differs.
 */

#include <cstdint>
#include <cstdio>
#include <cstring>
#include <mutex>

#include "argon2bitweb/argon2.h"     /* argon2_ctx, argon2_context, Argon2AutoDetectImpl, ... */
#include "sha512_dpowcoin.h"         /* CSHA512Dpowcoin */

#ifdef _WIN32
#  define MODULE_API __declspec(dllexport)
#else
#  define MODULE_API
#endif

/* -------------------------------------------------------------------------
 * One-time SIMD auto-detection (thread-safe via std::call_once).
 * Shared init path with argon2_bitweb.cpp: the dispatcher is process-global,
 * calling this again is a cheap no-op after the first call from either file.
 * ------------------------------------------------------------------------- */

static std::once_flag s_argon2_dpowcoin_init;

static void do_argon2_dpowcoin_init(void)
{
    const char *impl = Argon2AutoDetectImpl(
        static_cast<uint8_t>(argon2_implementation::USE_ALL));
    fprintf(stdout,
        "[libmultihash] Argon2 dispatcher (dpowcoin): selected implementation = %s\n",
        impl);
    fflush(stdout);
}

static inline void ensure_argon2_dpowcoin_init(void)
{
    std::call_once(s_argon2_dpowcoin_init, do_argon2_dpowcoin_init);
}

/* -------------------------------------------------------------------------
 * Internal helper — fills argon2_context and runs argon2_ctx() with an
 * explicit (possibly distinct) pwd/salt pair, unlike Bitweb's pwd==salt.
 * ------------------------------------------------------------------------- */
static void argon2_dpowcoin_round(
        const void   *pwd,     uint32_t pwd_len,
        const void   *salt,    uint32_t salt_len,
        void         *output,  uint32_t output_len,
        uint32_t      t_cost,  uint32_t m_cost,
        uint32_t      lanes,   uint32_t threads,
        uint32_t      version)
{
    argon2_context ctx;
    memset(&ctx, 0, sizeof(ctx));

    ctx.out       = static_cast<uint8_t*>(output);
    ctx.outlen    = output_len;
    ctx.pwd       = const_cast<uint8_t*>(static_cast<const uint8_t*>(pwd));
    ctx.pwdlen    = pwd_len;
    ctx.salt      = const_cast<uint8_t*>(static_cast<const uint8_t*>(salt));
    ctx.saltlen   = salt_len;
    ctx.t_cost    = t_cost;
    ctx.m_cost    = m_cost;
    ctx.lanes     = lanes;
    /* threads: execution parallelism only, NOT consensus-critical.
     * Safe to run up to `lanes` here — output is identical to threads=1
     * for a correct Argon2 implementation as long as threads <= lanes. */
    ctx.threads   = threads;
    ctx.version   = version;
    ctx.flags     = ARGON2_DEFAULT_FLAGS;

    int rc = argon2_ctx(&ctx, Argon2_id);
    (void)rc; /* fatal OOM/programming error - not expected in normal operation */
}

/* -------------------------------------------------------------------------
 * Export: Dpowcoin consensus Argon2id PoW hash (two-round, chained salt)
 *
 * Consensus-critical parameters — must match block.cpp exactly:
 *   Round 1: type=Argon2id, t=2, m=4096  KiB, lanes=2, version=0x13
 *   Round 2: type=Argon2id, t=2, m=32768 KiB, lanes=2, version=0x13
 *   salt(round1) = SHA512(SHA512(header))   [64 bytes]
 *   salt(round2) = output of round 1        [32 bytes]
 *   pwd (both rounds) = 80-byte serialised block header
 *
 * `threads` is NOT consensus-critical (see note above ctx.threads in
 * argon2_dpowcoin_round): it only controls how many CPU threads Argon2
 * itself uses to fill memory for a SINGLE hash. Output is identical for
 * any threads in [1, lanes] - lanes=2 here, so 1 or 2 are the only
 * meaningful values; anything higher is silently no better than 2 but
 * harmless (the backend just won't have more lanes to split across).
 * Passed through from C# so it's configurable per-deployment via
 * coins.json without touching this file.
 * ------------------------------------------------------------------------- */
extern "C" MODULE_API void argon2id_dpowcoin_export(
        const char *input,
        char       *output,
        uint32_t    input_len,   /* always 80 for PoW */
        uint32_t    threads)     /* execution parallelism only, see above */
{
    ensure_argon2_dpowcoin_init();

    /* salt = SHA-512(SHA-512(header)) */
    unsigned char tmp[64];
    unsigned char salt64[64];

    CSHA512Dpowcoin()
        .Write(reinterpret_cast<const unsigned char*>(input), input_len)
        .Finalize(tmp);

    CSHA512Dpowcoin()
        .Write(tmp, 64)
        .Finalize(salt64);

    /* Round 1 */
    unsigned char round1_out[32];
    argon2_dpowcoin_round(
        input, input_len,
        salt64, 64,
        round1_out, 32,
        /*t=*/2, /*m=*/4096, /*lanes=*/2, threads,
        ARGON2_VERSION_NUMBER);

    /* Round 2 — salt is round 1's output */
    argon2_dpowcoin_round(
        input, input_len,
        round1_out, 32,
        output, 32,
        /*t=*/2, /*m=*/32768, /*lanes=*/2, threads,
        ARGON2_VERSION_NUMBER);
}
