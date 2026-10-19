# Carry and select lowering

The patches in this directory modify [dotnet/runtime](https://github.com/dotnet/runtime) and are distributed under the [MIT License](LICENSE), copyright (c) .NET Foundation and Contributors.

Apply `followup.patch` to dotnet/runtime **0a303c9**, the merged PR #134039 head. The combined patch contains the carry-row follow-up and the inverse-select addition below; [draft PR #3](https://github.com/benaadams/runtime/pull/3) carries them as two commits. It changes no managed runtime or instruction-set APIs. The existing carry-row changes are:

* Recognize independent multiply-accumulate rows even when another product in the block is not a row lane. A square's diagonal products previously prevented its cross-product rows from using ADCX/ADOX. Rejected lanes remain untouched and terminate a candidate row; all original single-use, interference and flag-preservation checks still apply.
* Give a row's common multiplier a separate local when its current value has another direct read after that row, before a redefinition. This lets LSRA keep the copy in RDX while preserving the source for a later diagonal/row. Restricting the copy avoids extra intervals in ordinary CIOS products. `MixedCarryRows` and the existing multiplier-copy/pressure cases exercise both paths.
* Keep forwarding recognized carry flags when the optional final-limb search exhausts its allowance. Align that search's allowance with the existing 8,192-node full-adder limit. The former early return prevented all forwarding in a large block. `CarryForwardBudget` has twelve canonical six-word additions (twenty-four carry chains) in one block; existing `CompositeCarry` cases remain enabled. Full-adder candidate, liveness and extended-use budgets are unchanged.

## Inverse-select addition

[`inverse-selects.patch`](inverse-selects.patch) contains only the second commit, relative to the carry-row follow-up. Use it for review, or apply it after that follow-up; do not apply both this patch and the combined `followup.patch`.

* Run mask-select lowering in optimized AMD64 methods without requiring a carry chain. Binary-GCD loops have no carry additions and previously skipped this pass.
* Preserve existing direct SETCC/carry-flag selects; lower other `0 - (value & 1)` selects with TEST/CMOV. Save the tested value before any source overwrite; repeat TEST after flag-clobbering work and reuse it only across checked flag-preserving nodes. Every mask use must still be an eligible select. Arbitrary masks keep their bitwise meaning.
* Recognize the two outputs of a shared XOR-mask swap after CSE. Check all uses and local definitions, save both source operands before either output can overwrite them, and consume the original integer comparison flags or a tested low bit. Reject exposed/escaping locals, live-out deltas and intervening flag clobbers. The existing 65,536-node mask-scan budget and EH exclusion apply.

`MaskedSwap.cs` is the new regression test. It checks a 31-step packed-coefficient loop against a branching reference, 32/64-bit swaps, comparison and low-bit masks, flag clobbers, overwritten operands, escaping/non-boolean masks, live-out deltas, aliasing and EH. It is also included in the standalone runner alongside all 20 existing tests. The optimized inverse loop uses eight CMOVs and two TESTs; its only branch is the public loop backedge. The recorded standalone loop has 28 instructions per step versus blst's 22, counting from the loop label through the backedge; the remaining difference includes register copies, constant loads and a second TEST. Inlining removes the matrix out-parameter round trip.

Release and Checked JIT builds succeeded on Windows x64. All **21 tests passed** with the final Checked JIT in all ten configurations: tiering 0/1 crossed with default, `JitStress=2`, and `JitStressRegs=1/3/8`. The Release runner passed too. The regression runner returns 100 on success. Functional, generated-code and timing evidence is in [validation](../docs/constant-time-validation.md); measurements are in [performance](../docs/performance.md#fixed-binary-gcd-inverses).

## Build and reproduce

```powershell
git -C '<runtime>' apply '<BlsNet>/jit/followup.patch'
subst R: '<runtime>'
cmd /c "cd /d R:\ && R:\src\coreclr\build-runtime.cmd -x64 -release -component jit -ninja"
# Repeat with -checked for regression runs.
```

Copy each experimental JIT into a private testhost's `shared/Microsoft.NETCore.App/<version>/clrjit.dll`. Run all tests in every matrix configuration, clearing stress settings between runs. Run builds, tests and timings sequentially. Install the final Release JIT into the shared testhost only after validation.

The measured Release `clrjit.dll` has SHA-256 `6DE7EEC60C3D117ACC7DB637F5B7D14161E5E488598CA1C2CF7810E82FA70637`. The fixed binary-GCD inverse measurements require the combined patch. The retained EIP-2537 precompile capture requires only the original PR.
