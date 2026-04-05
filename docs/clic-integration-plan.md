# CLIC Integration Plan for ESP32-C3

**Status: COMPLETED (2026-04-05)**

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

## Final Architecture

```
Peripheral → GPIO → IntMatrix (source→line mapping) → CLIC inputs → CPU
                                                         ↓
                                                mcause = 0x80000000 | line
```

CLINT (timer) is also routed through CLIC: `[0, 1] -> clic@[3, 7]`

## Platform Configuration (esp32c3.repl)

```repl
cpu: CPU.RiscV32 @ sysbus
    cpuType: "rv32imc_zicsr_zifencei"
    privilegedArchitecture: PrivilegedArchitecture.PrivUnratified

clic: IRQControllers.CoreLocalInterruptController @ {
        sysbus <0x20000000, +0x10000>;
        cpu <0x1000, +0x4A1>
    }
    cpu: cpu
    numberOfInterrupts: 32
    machineLevelBits: 4
    supervisorLevelBits: 0
    modeBits: 0

clint: IRQControllers.CoreLevelInterruptor @ sysbus 0x60801000
    frequency: 16000000
    [0, 1] -> clic@[3, 7]

intmatrix: IRQControllers.ESP32C3_InterruptMatrix @ sysbus 0x600C2000
    [0-31] -> clic@[0-31]
```

## Boot Sequence

### Phase 1: Boot (1 second)
CLIC is present but unconfigured — no CLIC interrupts enabled.
The firmware boots normally using standard vectored mode (mtvec mode=1).
PrivUnratified doesn't break boot (the previous failure was from
pre-configured CLIC interrupts firing during ROM init).

### Phase 2: CLIC activation (after boot)
1. Set `cpu MTVEC 0x40380003` (firmware vector table + CLIC mode=3)
2. Add dispatch hook at 0x40380000 (vector table entry 0):
   - If mcause bit 31 set → redirect to `_interrupt_handler` (0x403801DC)
   - If mcause bit 31 clear → fall through to `_panic_handler`
3. Enable all 32 CLIC interrupts: IE=1, ATTR=0x00 (SHV=0, level-triggered), CTL=0xFF
4. Set `cpu WfiAsNop true` to avoid idle loop hangs
5. CLIC + SYSTIMER deliver interrupts naturally — no manual kick needed

## Workarounds Eliminated

| Workaround | How CLIC eliminates it |
|---|---|
| W.5 MIE force | Firmware sets MSTATUS.MIE during init; CLIC handles per-interrupt enable |
| W.6 Manual kick | SYSTIMER fires naturally through intmatrix → CLIC → CPU |
| W.7 mcause hook | CLIC sets mcause = 0x80000000 \| cpu_line automatically |

## Key Technical Findings

### mtvec must be set via Renode API, not csrw
The CLIC's `IIndirectCSRPeripheral` intercepts `csrw mtvec` but doesn't
update the TLIB's raw CPU register. The TLIB checks the raw register for
CLIC mode (mtvec[1:0]=3). Using `cpu MTVEC 0x40380003` via the Renode API
sets the raw register directly, which is what the CPU actually checks.

### SHV=0 (non-vectored) is the working mode
SHV=1 (vectored) has a Renode TLIB issue where the CPU doesn't take
vectored CLIC interrupts despite the CLIC presenting them. SHV=0 works
reliably — all interrupts jump to mtvec_base, and a Python dispatch hook
routes based on mcause bit 31 (interrupt vs exception).

### PrivUnratified is safe during boot
The ESP-IDF firmware writes `mtvec = _vector_table | 1` (mode=1, standard
vectored). With PrivUnratified, mode=1 is handled identically to Priv1_10.
The original boot failure was caused by pre-enabled CLIC interrupts firing
during ROM init — not by PrivUnratified CSR behaviour changes.

### CLIC is invisible to firmware
The firmware reads/writes interrupt matrix registers at 0x600C2000
(CPU_INT_ENABLE, CPU_INT_PRI, CPU_INT_THRESH). It never accesses CLIC
registers directly. The CLIC acts purely as the CPU interrupt delivery
mechanism, transparent to firmware.

## Sources

- [Antmicro CLIC blog post](https://antmicro.com/blog/2024/09/fast-interrupt-controller-for-risc-v-simulated-in-renode)
- [CoreLocalInterruptController.cs](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Cores/RiscV/CoreLocalInterruptController.cs)
- [CLIC Robot tests](https://github.com/renode/renode/tree/master/tests/peripherals/CLIC)
- [CLIC spec](https://github.com/riscv/riscv-fast-interrupt/blob/v0.10/src/clic.adoc)
