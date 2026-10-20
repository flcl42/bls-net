/* Test-only exports around the pinned blst assembly. Production never links this file. */
#include <stdint.h>

#if defined(__x86_64__) && defined(__ADX__)
# define raw_product mulx_384
# define raw_reduce redcx_mont_384
#else
# define raw_product mul_384
# define raw_reduce redc_mont_384
#endif

#if defined(_WIN32)
# define EXPORT __declspec(dllexport)
#else
# define EXPORT __attribute__((visibility("default")))
#endif

extern void raw_product(uint64_t out[12], const uint64_t a[6], const uint64_t b[6]);
extern void raw_reduce(uint64_t out[6], const uint64_t in[12], const uint64_t p[6], uint64_t n0);
extern void sub_mod_384x384(uint64_t out[12], const uint64_t a[12], const uint64_t b[12], const uint64_t p[6]);

static const uint64_t modulus[6] = {
    UINT64_C(0xb9feffffffffaaab), UINT64_C(0x1eabfffeb153ffff),
    UINT64_C(0x6730d2a0f6b0f624), UINT64_C(0x64774b84f38512bf),
    UINT64_C(0x4b1ba7b6434bacd7), UINT64_C(0x1a0111ea397fe69a)
};

EXPORT void bls_net_mul_384(uint64_t out[12], const uint64_t a[6], const uint64_t b[6])
{
    raw_product(out, a, b);
}

/* Contract: 0 <= in < pR. This is the same bounded REDC used by blst's tower. */
EXPORT void bls_net_redc_384(uint64_t out[6], const uint64_t in[12])
{
    raw_reduce(out, in, modulus, UINT64_C(0x89f3fffcfffcfffd));
}

/* Contract: a,b in [0,pR). The assembly subtraction represents a-b modulo pR. */
EXPORT void bls_net_sub_redc_384(uint64_t out[6], const uint64_t a[12], const uint64_t b[12])
{
    uint64_t difference[12];
    sub_mod_384x384(difference, a, b, modulus);
    raw_reduce(out, difference, modulus, UINT64_C(0x89f3fffcfffcfffd));
}
