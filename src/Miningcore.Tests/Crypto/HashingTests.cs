using System;
using System.Linq;
using System.Text;
using Miningcore.Crypto.Hashing.Algorithms;
using Miningcore.Extensions;
using Miningcore.Tests.Util;
using Xunit;
using Xunit.Abstractions;

namespace Miningcore.Tests.Crypto;

public class HashingTests : TestBase
{
    private static readonly byte[] testValue = Enumerable.Repeat((byte) 0x80, 32).ToArray();

    // some algos need 80 byte input buffers
    private static readonly byte[] testValue2 = Enumerable.Repeat((byte) 0x80, 80).ToArray();

    // 80-byte headers for Argon2 Bitweb / Dpowcoin tests
    private static readonly byte[] header80Zero = new byte[80];
    private static readonly byte[] header80AB   = Enumerable.Repeat((byte) 0xAB, 80).ToArray();

    // Real Dpowcoin mainnet block #226913 header (version, prevhash, merkleroot, time, bits, nonce),
    // taken from `getblock <hash> false` (raw 80-byte serialization, first 160 hex chars).
    private static readonly byte[] dpowcoinBlock226913Header = "00000020cb76eeb04d059827c04687be643fcaf34867ae088346db2d280bf23295868cc644e15610989cf693f8fc2571dd63ec030a7c19c49848a68652b73805fb2c877663f7716a0b6e461e84aaaac2".HexToByteArray();

    // Expected result of the node RPC `getargon2idpowhash 226913`, as displayed (reversed/uint256 order).
    private const string dpowcoinBlock226913ExpectedPowHash = "00001717c8bb40cc46f5af4cf0db4ac7c88d89a5bc856cf82b3facc5a6a9900e";

    // Second real Dpowcoin mainnet block, near-genesis (height 10000, easy difficulty) - deliberately
    // far from block226913 in height/difficulty/nonce range to catch anything that only happens to
    // work for one region of the search space.
    private static readonly byte[] dpowcoinBlock10000Header = "00000020f7bd86f3b3d508a595c526307264404880c2317bbf381c9fcd46535ae18f815e90021192dd550117e9517fd4318f0a463f63fa4841a2fc817e4fc709a6d0dceec0a14d665d00031f3d080000".HexToByteArray();
    private const string dpowcoinBlock10000ExpectedPowHash = "0001e12825eaf426aba8a202071d1520ce5a9825856a27b6c5c949ba2c950965";

    // Third real Dpowcoin mainnet block - chain tip at time of writing (height 228175).
    private static readonly byte[] dpowcoinBlock228175Header = "00000020a7e4cf2cf9324e2ff829956ce75d995768e8a36518e226573c571bb46d630e607a824ddbbf6ac71e79047b00191f5f8649fa71eb56fe7661352c0ce3e6f1abe35096776aa40b351e080000c0".HexToByteArray();
    private const string dpowcoinBlock228175ExpectedPowHash = "00001097f5a8a769d469c3e625d53021a91313b081279000f7f58f1adf922eed";

    [Fact]
    public void Scrypt_Hash()
    {
        var hasher = new Scrypt(1024, 1);
        var hash = new byte[32];
        hasher.Digest(testValue, hash);
        var result = hash.ToHexString();

        Assert.Equal("b546d334422ff5fff98e8ba847a55bbc06271c64bb5e21107b1b225f6579d40a", result);
    }

    [Fact]
    public void Sha256D_Hash()
    {
        var hasher = new Sha256D();
        var hash = new byte[32];
        hasher.Digest(testValue, hash);
        var result = hash.ToHexString();

        Assert.Equal("4f4eb6dbba8198745a278997e154e8309b571259e33fce4d3a31adea39dc9173", result);
    }

    [Fact]
    public void Sha256DT_Hash()
    {
        var hasher = new Sha256DT();
        var hash = new byte[32];
        hasher.Digest(testValue2, hash);
        var result = hash.ToHexString();
        Assert.Equal("bf4735b3a0feebe83727a7a2327f8223eec7484190e8dd52611ce75b045a2e75", result);
    }

    [Fact]
    public void Sha256S_Hash()
    {
        var hasher = new Sha256S();
        var hash = new byte[32];
        hasher.Digest(testValue, hash);
        var result = hash.ToHexString();

        Assert.Equal("bd75a82b9957d6d043076dea52262635042693f1fe23bcadadaecc908e1e5cc6", result);
    }

    [Fact]
    public void Sha512256D_Hash()
    {
        var hasher = new Sha512256D();
        var hash = new byte[32];
        hasher.Digest(testValue, hash);
        var result = hash.ToHexString();

        Assert.Equal("6b86ce4bf945d8e935d51db4e32589acf6dbcda58ca1cef7568d52f704c46d7f", result);
    }

    [Fact]
    public void Heavy_Hash()
    {
        var hasher = new HeavyHash();
        var hash = new byte[32];
        hasher.Digest(testValue, hash);
        var result = hash.ToHexString();

        Assert.Equal("e89c26771f3fda42e6f8ed82ca888f805fa15013d8543ab2692904095c6d3dc3", result);
    }

    [Fact]
    public void DigestReverser_Hash()
    {
        var hasher = new DigestReverser(new Sha256S());
        var hash = new byte[32];
        hasher.Digest(testValue, hash);
        var result = hash.ToHexString();

        Assert.Equal("c65c1e8e90ccaeadadbc23fef193260435262652ea6d0743d0d657992ba875bd", result);
    }

    [Fact]
    public void DummyHasher_Should_Always_Throw()
    {
        var hasher = new Null();
        Assert.Throws<InvalidOperationException>(() => hasher.Digest(new byte[23], null));
        Assert.Throws<InvalidOperationException>(() => hasher.Digest(null, null));
    }

    //   Argon2id, t=3, m=1024 KiB, lanes=1, version=0x13, pwd==salt==input.

    [Fact]
    public void Argon2idBitweb_KnownVector_AllZero()
    {
        var hasher = new Argon2idBitweb();
        var hash = new byte[32];
        hasher.Digest(header80Zero, hash);
        Assert.Equal("3bb15018af629a335077c8c15412d2830e8c67452fa9a54ac87165024a910a8f", hash.ToHexString());
    }

    [Fact]
    public void Argon2idBitweb_KnownVector_AB()
    {
        var hasher = new Argon2idBitweb();
        var hash = new byte[32];
        hasher.Digest(header80AB, hash);
        Assert.Equal("d3b994430326a33cf1fcdab01cfcb957b0322adf90a3dca22ace8f94dd4666fb", hash.ToHexString());
    }

    [Fact]
    public void Argon2idBitweb_Deterministic()
    {
        var hasher = new Argon2idBitweb();
        var hash1 = new byte[32];
        var hash2 = new byte[32];
        hasher.Digest(header80AB, hash1);
        hasher.Digest(header80AB, hash2);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Argon2idBitweb_DifferentInput_DifferentHash()
    {
        var hasher = new Argon2idBitweb();
        var hash1 = new byte[32];
        var hash2 = new byte[32];
        hasher.Digest(header80Zero, hash1);
        hasher.Digest(header80AB,   hash2);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Argon2idBitweb_OutputNonZero()
    {
        var hasher = new Argon2idBitweb();
        var hash = new byte[32];
        hasher.Digest(header80Zero, hash);
        Assert.False(hash.All(b => b == 0), "Hash must not be all-zero");
    }

    [Fact]
    public void Argon2Generic_BitwebParams_EqualsArgon2idBitweb()
    {
        // argon2_generic with Bitweb consensus params must produce identical output
        var bitweb  = new Argon2idBitweb();
        var generic = new Argon2Generic();

        var expected = new byte[32];
        var actual   = new byte[32];

        bitweb.Digest(header80AB, expected);
        // extra[] order: tCost, mCost, lanes, typeId(2=Argon2id), version(0x13)
        generic.Digest(header80AB, actual, 3u, 1024u, 1u, 2u, 0x13u);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Argon2Generic_DifferentParams_DifferentHash()
    {
        var generic = new Argon2Generic();
        var hash1 = new byte[32];
        var hash2 = new byte[32];

        // Bitweb params
        generic.Digest(header80AB, hash1, 3u, 1024u, 1u, 2u, 0x13u);
        // Different: Argon2d, t=1, m=256
        generic.Digest(header80AB, hash2, 1u,  256u, 1u, 0u, 0x13u);

        Assert.NotEqual(hash1, hash2);
    }

    // Dpowcoin: two-round Argon2id, chained salt.
    //   salt(round1) = SHA512(SHA512(header))                          [64 bytes]
    //   Round 1: t=2, m=4096  KiB, lanes=2, v=0x13 -> 32 bytes (becomes round-2 salt)
    //   Round 2: t=2, m=32768 KiB, lanes=2, v=0x13 -> 32 bytes (final PoW hash)
    //   pwd (both rounds) = 80-byte serialised block header.
    //
    // AllZero/AllAB vectors below were generated from this same native build
    // (src/Native/libmultihash, argon2id_dpowcoin_export) and only guard against
    // regressions in this repo. Block226913 is the real cross-check: header,
    // parameters and salt-chaining all validated against the node's own
    // `getargon2idpowhash` RPC output on live chain data.

    [Fact]
    public void Argon2idDpowcoin_KnownVector_AllZero()
    {
        var hasher = new Argon2idDpowcoin();
        var hash = new byte[32];
        hasher.Digest(header80Zero, hash);
        Assert.Equal("d9f2447f5f36e8f576bf62682b8ba2fea3ebbbd122b0b3c1c9dab5ecdace2cf1", hash.ToHexString());
    }

    [Fact]
    public void Argon2idDpowcoin_KnownVector_AB()
    {
        var hasher = new Argon2idDpowcoin();
        var hash = new byte[32];
        hasher.Digest(header80AB, hash);
        Assert.Equal("1704f2f510f3a6edd064a23df69e44bec95d7f78c84fbd3ba8207f8fd01d6bfc", hash.ToHexString());
    }

    [Fact]
    public void Argon2idDpowcoin_Deterministic()
    {
        var hasher = new Argon2idDpowcoin();
        var hash1 = new byte[32];
        var hash2 = new byte[32];
        hasher.Digest(header80AB, hash1);
        hasher.Digest(header80AB, hash2);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Argon2idDpowcoin_DifferentInput_DifferentHash()
    {
        var hasher = new Argon2idDpowcoin();
        var hash1 = new byte[32];
        var hash2 = new byte[32];
        hasher.Digest(header80Zero, hash1);
        hasher.Digest(header80AB,   hash2);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Argon2idDpowcoin_OutputNonZero()
    {
        var hasher = new Argon2idDpowcoin();
        var hash = new byte[32];
        hasher.Digest(header80Zero, hash);
        Assert.False(hash.All(b => b == 0), "Hash must not be all-zero");
    }

    [Fact]
    public void Argon2idDpowcoin_DiffersFromBitweb()
    {
        // Sanity check that the two coins' hashers are not accidentally aliased
        // to the same native export (different params/salt-chaining => different output).
        var bitweb   = new Argon2idBitweb();
        var dpowcoin = new Argon2idDpowcoin();

        var hash1 = new byte[32];
        var hash2 = new byte[32];
        bitweb.Digest(header80AB, hash1);
        dpowcoin.Digest(header80AB, hash2);

        Assert.NotEqual(hash1, hash2);
    }

    // Cross-checked against live Dpowcoin mainnet data across three widely-spaced blocks
    // (near-genesis low-difficulty, a mid-height block, and the chain tip at time of writing) -
    // node RPC `getargon2idpowhash <height>` for each. The hasher returns raw (non-reversed)
    // bytes, same convention as Argon2idBitweb - the node displays uint256 values byte-reversed,
    // so we reverse before comparing to the RPC string.
    [Fact]
    public void Argon2idDpowcoin_RealBlock_10000()
    {
        var hasher = new Argon2idDpowcoin();
        var hash = new byte[32];
        hasher.Digest(dpowcoinBlock10000Header, hash);

        var displayed = hash.Reverse().ToArray().ToHexString();
        Assert.Equal(dpowcoinBlock10000ExpectedPowHash, displayed);
    }

    [Fact]
    public void Argon2idDpowcoin_RealBlock_226913()
    {
        var hasher = new Argon2idDpowcoin();
        var hash = new byte[32];
        hasher.Digest(dpowcoinBlock226913Header, hash);

        var displayed = hash.Reverse().ToArray().ToHexString();
        Assert.Equal(dpowcoinBlock226913ExpectedPowHash, displayed);
    }

    [Fact]
    public void Argon2idDpowcoin_RealBlock_228175()
    {
        var hasher = new Argon2idDpowcoin();
        var hash = new byte[32];
        hasher.Digest(dpowcoinBlock228175Header, hash);

        var displayed = hash.Reverse().ToArray().ToHexString();
        Assert.Equal(dpowcoinBlock228175ExpectedPowHash, displayed);
    }

    // `threads` (coins.json headerHasher.args[0], see coins.json "dpowcoin") is execution
    // parallelism only, NOT consensus-critical - both rounds use lanes=2, so output must be
    // byte-identical for any threads count. This is the guarantee that makes it safe to tune
    // per-deployment (more CPU cores / RAM on a beefier pool server) without touching native
    // code or coordinating a consensus change with the coin. Verified directly against real
    // chain data, not just self-consistency, so a backend that quietly ignores `threads` or
    // handles it incorrectly for some value would still be caught.
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(4u)]
    [InlineData(8u)]
    public void Argon2idDpowcoin_ThreadsParam_DoesNotChangeOutput_RealBlock(uint threads)
    {
        var hasher = new Argon2idDpowcoin(threads);
        var hash = new byte[32];
        hasher.Digest(dpowcoinBlock226913Header, hash);

        var displayed = hash.Reverse().ToArray().ToHexString();
        Assert.Equal(dpowcoinBlock226913ExpectedPowHash, displayed);
    }

    [Fact]
    public void Argon2idDpowcoin_DefaultThreads_Is1()
    {
        // coins.json can omit "args" entirely and still get correct, if slower, hashing.
        var explicitThread1 = new Argon2idDpowcoin(1);
        var defaultCtor     = new Argon2idDpowcoin();

        var hash1 = new byte[32];
        var hash2 = new byte[32];
        explicitThread1.Digest(header80AB, hash1);
        defaultCtor.Digest(header80AB, hash2);

        Assert.Equal(hash1, hash2);
    }
}
