# Constant-time cofactor additions

The optimized additions require on-curve points. They remain valid on the full curve, including its cofactor subgroup and every representation of infinity. General secret point addition retains doubling handling.

Let `z = -0xd201000000010000`, `r = z^4-z^2+1`, `h1 = (z-1)^2/3`, `p = h1*r+z`, and

```
h2 = (z^8 - 4z^7 + 5z^6 - 4z^4 + 6z^3 - 4z^2 - 4z + 13) / 9.
```

The curve orders are `N1 = h1*r` and `N2 = h2*r`. The twist cofactor and equation are independently tabulated in [CFRG pairing-friendly-curves-14, section 4.2.1](https://www.ietf.org/archive/id/draft-irtf-cfrg-pairing-friendly-curves-14.html#section-4.2.1). `CofactorScheduleTests` checks the polynomial against that hexadecimal cofactor, checks the group orders on full-curve points, and verifies every gcd below with BigInteger.

In the fixed `TimesMinusZ` chain, the accumulator before each addition of `P` is `[k]P`, with `k` equal to `2`, `0xc`, `0x68`, `0xd200`, or `0xd20100000000`. Every `gcd(k-1,N2)` is one. Thus `[k]P = P` implies `P` is infinity. This allows all five twist additions to use the ordinary projective formula with masked infinity handling. G1 retains the complete formula in this chain: two of its gcds are 11 and 3. The final addition of `P` to `[-z]P` has `gcd(-z-1,N1) = gcd(-z-1,N2) = 1` and can use the distinct-point formula on either curve.

For the remaining twist cofactor schedule, write `psi` for the transported p-power Frobenius. Its characteristic equation is `psi^2 - t*psi + [p] = 0`, with `t = z+1`. For `D = [a] + [b]*psi`, its conjugate satisfies

```
D * ([a+b*t] - [b]*psi) = [a^2 + a*b*t + b^2*p].
```

If this integer norm is coprime to `N2`, the kernel of D on the rational twist group is trivial. Apply this to the difference between the two operands at each addition:

| Addition | a | b | gcd(norm, N2) |
|---|---|---|---|
| `2*psi^2(P) + (-P)` | `1-2p` | `2t` | 1 |
| `(2*psi^2(P)-P) + (-psi(P))` | `-2p-1` | `2t+1` | 13 |
| `[1-z]P + (-psi(P))` | `1-z` | 1 | 1 |
| `(2*psi^2(P)-P-psi(P)) + ([-z]*([1-z]P-psi(P)))` | `-2p-1-z^2+z` | `2t-1-z` | 1 |

Only the second addition retains doubling handling. The distinct-point helper still selects both infinity cases using masks and naturally returns Z=0 for inverse operands. Its arithmetic, addresses and instruction count never branch on point values.

Tests generate full twist points by solving its equation rather than mapping into the prime subgroup. They check the Frobenius equation, fixed -z chain, aliases and cofactor clearing against binary multiplication by the independent 636-bit effective cofactor in blst/RFC 9380. Native binary group operations supply another reference. The group order contains 13^2; tests project onto 13-torsion and then the Frobenius eigenvalue-7 line, explicitly exercising the second addition as an actual doubling. This case must keep the complete formula.
