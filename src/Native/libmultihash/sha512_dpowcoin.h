// Adapted from dpowcoin/src/crypto/sha512.h (Bitcoin Core-derived, MIT licensed).
// Renamed CSHA512 -> CSHA512Dpowcoin to avoid any symbol collision with other
// hash code in this library, and to make clear this instance is used only
// for the dpowcoin Argon2id salt derivation, not as a general-purpose hasher.

#ifndef LIBMULTIHASH_SHA512_DPOWCOIN_H
#define LIBMULTIHASH_SHA512_DPOWCOIN_H

#include <cstdint>
#include <cstdlib>

/** A hasher class for SHA-512 (full 64-byte digest, standard IV). */
class CSHA512Dpowcoin
{
private:
    uint64_t s[8];
    unsigned char buf[128];
    uint64_t bytes{0};

public:
    static constexpr size_t OUTPUT_SIZE = 64;

    CSHA512Dpowcoin();
    CSHA512Dpowcoin& Write(const unsigned char* data, size_t len);
    void Finalize(unsigned char hash[OUTPUT_SIZE]);
    CSHA512Dpowcoin& Reset();
    uint64_t Size() const { return bytes; }
};

#endif // LIBMULTIHASH_SHA512_DPOWCOIN_H
