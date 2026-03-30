# CLIC Integration Plan for ESP32-C3

## Discovery

Renode has a **fully functional CLIC implementation** since v1.16.0 (Sep 2024).
This was missed in the initial research. The key class is
`IRQControllers.CoreLocalInterruptController` in renode-infrastructure.

## How CLIC Works in Renode

1. CLIC registers as `IIndirectCSRPeripheral` — intercepts mcause, mtvec CSR access
2. GPIO inputs trigger interrupt pending bits
3. `UpdateInterrupt()` selects highest-priority pending interrupt
4. `cpu.ClicPresentInterrupt(interruptNumber, vectored, level, privilege)` delivers it
5. The CPU takes the interrupt with mcause = 0x80000000 | interruptNumber
6. This is EXACTLY what ESP32-C3 firmware expects!

## .repl Configuration Example

```
cpu: CPU.RiscV32 @ sysbus
    privilegedArchitecture: PrivilegedArchitecture.PrivUnratified

clic: IRQControllers.CoreLocalInterruptController @ {
        sysbus <0x04000000, +0x10000>;
        cpu <0x1000, +0x4A1>
    }
    cpu: cpu
    machineLevelBits: 8
    supervisorLevelBits: 8
    modeBits: 0
```

## ESP32-C3 Integration Approach

The ESP32-C3's interrupt architecture is:
```
Peripheral → Interrupt Matrix (source mapping) → CPU int lines → ISR dispatch
```

Currently in Renode:
```
Peripheral → GPIO → IntMatrix.OnGPIO → MEIP (GPIO 11) → CPU
Python hook overrides mcause after csrr instruction
```

With CLIC, the architecture becomes:
```
Peripheral → GPIO → IntMatrix.OnGPIO → CLIC inputs → CPU
CLIC automatically sets mcause = 0x80000000 | cpu_line_number
```

### Changes Needed

1. **CPU architecture**: Change from `PrivilegedArchitecture.Priv1_10` to
   `PrivilegedArchitecture.PrivUnratified` (required for CLIC CSR support)

2. **Add CLIC instance**: Wire between interrupt matrix and CPU
   ```
   clic: IRQControllers.CoreLocalInterruptController @ {
           sysbus <address>;
           cpu <csr_base, +size>
       }
       cpu: cpu
       numberOfInterrupts: 32
       machineLevelBits: 4
       modeBits: 0
   ```

3. **Interrupt Matrix output**: Instead of `11 -> cpu@11`, wire each CPU
   interrupt line to CLIC inputs: `[0-31] -> clic@[0-31]`

4. **Remove workarounds**:
   - W.5: MIE/MSTATUS force → CLIC handles its own enable/threshold
   - W.7: mcause Python hook → CLIC sets mcause correctly
   - W.6: Manual interrupt kick → CLIC delivers interrupts directly

### Risks

- ESP32-C3's interrupt matrix is NOT a standard CLIC — it has its own register
  layout at 0x600C2000. The CLIC in Renode has a different register layout.
  The firmware writes to interrupt matrix registers (CPU_INT_ENABLE, CPU_INT_PRI,
  CPU_INT_THRESH) which don't exist in the standard CLIC.

- Solution: Keep the ESP32C3_InterruptMatrix C# peripheral for register access,
  but wire its OUTPUT to CLIC inputs instead of directly to the CPU. The
  interrupt matrix handles source→line mapping and priority, then CLIC handles
  the CPU-facing interrupt delivery with proper mcause.

- The firmware reads/writes to interrupt matrix registers directly, not CLIC
  registers. So CLIC must be "invisible" to firmware — it only acts as the
  CPU interrupt delivery mechanism.

## Implementation Steps

1. Add CLIC to esp32c3.repl with 32 interrupt inputs
2. Change interrupt matrix to output to CLIC instead of CPU GPIO 11
3. Change CPU privilegedArchitecture to PrivUnratified
4. Remove CLINT (or keep it for timer, separate from CLIC)
5. Test: does firmware boot and receive interrupts?
6. If yes: remove mcause hook, MIE force, manual kick
7. Run Robot tests to verify

## Sources

- [Antmicro CLIC blog post](https://antmicro.com/blog/2024/09/fast-interrupt-controller-for-risc-v-simulated-in-renode)
- [CoreLocalInterruptController.cs](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Cores/RiscV/CoreLocalInterruptController.cs)
- [CLIC Robot tests](https://github.com/renode/renode/tree/master/tests/peripherals/CLIC)
- [CLIC spec](https://github.com/riscv/riscv-fast-interrupt/blob/v0.10/src/clic.adoc)
