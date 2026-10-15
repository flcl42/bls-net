# Carry lowering follow-up

Apply `followup.patch` to dotnet/runtime **0a303c9**, the merged PR #134039 head used for the baseline. It changes `lower.cpp` and adds `MixedCarryRows` and `CarryForwardBudget` under `src/tests/JIT/opt/Add/`. It has no managed runtime or instruction-set API changes.

* Recognize independent multiply-accumulate rows even when another product in the block is not a row lane. A square's diagonal products previously prevented its cross-product rows from using ADCX/ADOX. Rejected lanes remain untouched and terminate a candidate row; all original single-use, interference and flag-preservation checks still apply.
* Give a row's common multiplier a separate local when its current value has another direct read after that row, before a redefinition. This lets LSRA keep the copy in RDX while preserving the source for a later diagonal/row. Restricting the copy avoids extra intervals in ordinary CIOS products. `MixedCarryRows` and the existing multiplier-copy/pressure cases exercise both paths.
* Keep forwarding recognized carry flags when the optional final-limb search exhausts its allowance. Align that search's allowance with the existing 8,192-node full-adder limit. The former early return prevented all forwarding in a large block. `CarryForwardBudget` has twelve canonical six-word additions (twenty-four carry chains) in one block; existing `CompositeCarry` cases remain enabled. Full-adder candidate, liveness and extended-use budgets are unchanged.

Release and Checked builds succeeded on Windows x64. The supplied standalone runner at `C:/Temp/blst-speedy/jittests` includes both new files through its default Compile glob. All **20 tests passed** under the final Checked JIT in all ten configurations: tiering 0/1 crossed with default, `JitStress=2`, and `JitStressRegs=1/3/8`. The Release runner passed too. Default Release disassembly verifies that `Mixed` gains twelve ADCX and fourteen ADOX instructions; `LargeBlock` drops from 120 SETcc instructions to twelve. Its high-word drains stay before array bounds checks so the rows are actually matched. Functional and constant-time results are in [validation](../docs/constant-time-validation.md); measurements are in [performance](../docs/performance.md#fused-maps-on-the-carry-aware-runtime).

Build in a fresh runtime checkout containing that base:

```powershell
git -C '<runtime>' apply '<BlsNet>/jit/followup.patch'
subst R: '<runtime>'
cmd /c "cd /d R:\ && R:\src\coreclr\build-runtime.cmd -x64 -release -component jit -ninja"
# Repeat with -checked for regression runs.
```

Copy each JIT into a private copy of the testhost's `shared/Microsoft.NETCore.App/<version>/clrjit.dll` while experimenting. Run the existing JIT tests plus the two new files with each matrix configuration; the standalone runner returns 100 on success. Clear stress variables between configurations. Do not combine builds, tests and timings concurrently.

The final Release JIT was installed into `C:/Temp/blst-speedy/rpr/artifacts/bin/testhost/net11.0-windows-Release-x64` after validation. Its `shared/Microsoft.NETCore.App/12.0.0/clrjit.dll` SHA-256 is `2E37927C2F21F22923B770DC1F67FF32401CDEC491267BEFB09B6D9F285522D8`. Current map results require this follow-up; the older retained precompile captures only require the original PR.
