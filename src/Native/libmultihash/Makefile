# ---------------------------------------------------------------------------
# libmultihash Makefile
#
# Algos: SHA256 (BTC), Scrypt (LTC), Argon2id (Bitweb),
#        YesCrypt + YesPower.
#
# Argon2 engine: Bitweb's optimised C++ with runtime SIMD dispatch.
#
# x86-64: all ISA variants compiled; dispatcher selects best at runtime:
#           AVX-512 > AVX2 > SSSE3 > SSE2 > ref
#
# aarch64/arm: x86 SIMD files skipped; ref.cpp provides Argon2AutoDetectImpl
#              with optional NEON path (compiled via -DENABLE_ARGON2_NEON).
#
# Other arches: ref-only (safe fallback, no special compile flags needed).
# ---------------------------------------------------------------------------

ARCH := $(shell uname -m)

BASE_CFLAGS   = -g -Wall -c -fPIC -O2 \
                -Wno-pointer-sign -Wno-char-subscripts \
                -Wno-unused-variable -Wno-unused-function \
                -Wno-strict-aliasing -Wno-discarded-qualifiers \
                -Wno-unused-const-variable \
                $(CPU_FLAGS) $(HAVE_FEATURE)

BASE_CXXFLAGS = -g -Wall -fPIC -fpermissive -O2 -std=c++11 \
                -Wno-char-subscripts -Wno-unused-variable \
                -Wno-unused-function -Wno-strict-aliasing \
                -Wno-sign-compare \
                $(CPU_FLAGS) $(HAVE_FEATURE)

CFLAGS   = $(BASE_CFLAGS)
CXXFLAGS = $(BASE_CXXFLAGS)

LDFLAGS = -shared
LDLIBS  = -lpthread
TARGET  = libmultihash.so

# ---------- Common objects (all architectures) ----------------------------

OBJECTS_COMMON = \
	exports.o scryptn.o sha256dt.o sha512_256.o \
	heavyhash/heavyhash.o heavyhash/keccak_tiny.o \
	yespower/crypto/blake2b-yp.o yespower/yespower-blake2b.o \
	yespower/yespower-combined.o yespower/yespower-platform.o

# ---------- Argon2 core C++ (no special SIMD flags — portable) -----------

OBJECTS_ARGON2_CORE = \
	argon2bitweb/argon2.o \
	argon2bitweb/argon2_core.o \
	argon2bitweb/argon2_encoding.o \
	argon2bitweb/argon2_thread.o \
	argon2bitweb/blake2/blake2b.o

# ref.cpp: always compiled.
#   On x86  → provides fill_segment_ref() used as safe fallback.
#   On !x86 → also provides fill_segment(), Argon2AutoDetectImpl() + NEON path.
OBJECTS_ARGON2_REF  = argon2bitweb/argon2_ref.o

# Pool-facing export wrapper
OBJECTS_ARGON2_EXPORT = argon2_bitweb.o sha512_dpowcoin.o argon2_dpowcoin.o

# ---------- Architecture-specific Argon2 SIMD objects ---------------------

ifeq ($(ARCH),x86_64)
  # Dispatcher: must see ALL ENABLE_ macros to emit the full CPUID branch tree.
  ARGON2_DISP_FLAGS = $(CXXFLAGS) \
      -DENABLE_ARGON2_SSE2 -DENABLE_ARGON2_SSSE3 \
      -DENABLE_ARGON2_AVX2 -DENABLE_ARGON2_AVX512

  OBJECTS_ARGON2_SIMD = \
      argon2bitweb/argon2_opt.o \
      argon2bitweb/argon2_opt_sse2.o \
      argon2bitweb/argon2_opt_ssse3.o \
      argon2bitweb/argon2_opt_avx2.o \
      argon2bitweb/argon2_opt_avx512.o

else ifeq ($(filter aarch64 arm,$(ARCH)),$(ARCH))
  # ARM: NEON only. ref.cpp is the dispatcher on this path.
  OBJECTS_ARGON2_SIMD = argon2bitweb/argon2_opt_neon.o

else
  # Other arch: ref only — no SIMD objects.
  OBJECTS_ARGON2_SIMD =
endif

# ---------- Final object list ---------------------------------------------

OBJECTS = \
	$(OBJECTS_COMMON) \
	$(OBJECTS_ARGON2_CORE) \
	$(OBJECTS_ARGON2_REF) \
	$(OBJECTS_ARGON2_SIMD) \
	$(OBJECTS_ARGON2_EXPORT)

# ---------- Main target ---------------------------------------------------

all: $(TARGET)

$(TARGET): $(OBJECTS)
	$(CXX) $(LDFLAGS) -o $@ $^ $(LDLIBS)
	@echo "[libmultihash] Built for arch=$(ARCH)"

# ---------- Generic compile rules ----------------------------------------

%.o: %.c
	$(CC) $(CFLAGS) -c -o $@ $<

%.o: %.cpp
	$(CXX) $(CXXFLAGS) -c -o $@ $<

# ---------- Argon2 x86 dispatcher ----------------------------------------

ifeq ($(ARCH),x86_64)
argon2bitweb/argon2_opt.o: argon2bitweb/argon2_opt.cpp
	$(CXX) $(ARGON2_DISP_FLAGS) -c -o $@ $<

argon2bitweb/argon2_opt_sse2.o: argon2bitweb/argon2_opt_sse2.cpp
	$(CXX) $(CXXFLAGS) -msse2 -DENABLE_ARGON2_SSE2 -c -o $@ $<

argon2bitweb/argon2_opt_ssse3.o: argon2bitweb/argon2_opt_ssse3.cpp
	$(CXX) $(CXXFLAGS) -mssse3 -DENABLE_ARGON2_SSSE3 -c -o $@ $<

argon2bitweb/argon2_opt_avx2.o: argon2bitweb/argon2_opt_avx2.cpp
	$(CXX) $(CXXFLAGS) -mavx2 -DENABLE_ARGON2_AVX2 -c -o $@ $<

argon2bitweb/argon2_opt_avx512.o: argon2bitweb/argon2_opt_avx512.cpp
	$(CXX) $(CXXFLAGS) -mavx512f -DENABLE_ARGON2_AVX512 -c -o $@ $<
endif

# ---------- Argon2 ARM NEON ----------------------------------------------

ifeq ($(filter aarch64 arm,$(ARCH)),$(ARCH))
argon2bitweb/argon2_opt_neon.o: argon2bitweb/argon2_opt_neon.cpp
	$(CXX) $(CXXFLAGS) -mfpu=neon -DENABLE_ARGON2_NEON -c -o $@ $<
endif

# ---------- Export wrapper -----------------------------------------------

sha512_dpowcoin.o: sha512_dpowcoin.cpp sha512_dpowcoin.h

argon2_dpowcoin.o: argon2_dpowcoin.cpp sha512_dpowcoin.h

argon2_bitweb.o: argon2_bitweb.cpp
	$(CXX) $(CXXFLAGS) -c -o $@ $<

.PHONY: clean

clean:
	$(RM) $(TARGET) $(OBJECTS) $(OBJECTS_ARGON2_SIMD)
