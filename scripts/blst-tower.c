/* Test-only entry points for the internal blst Fp6 formulas. */
#include "../external/blst/src/server.c"

__declspec(dllexport) void blst_fp6_mul(vec384fp6 ret,
                                      const vec384fp6 a,
                                      const vec384fp6 b)
{
    mul_fp6(ret, a, b);
}

__declspec(dllexport) void blst_fp6_sqr(vec384fp6 ret, const vec384fp6 a)
{
    sqr_fp6(ret, a);
}
