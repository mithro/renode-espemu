# ESP32 Peripheral Emulation: Self-Sustaining Iterative Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan. Use superpowers:using-git-worktrees for branch isolation. Use superpowers:test-driven-development for all implementation. Use superpowers:verification-before-completion before any merge.

**Goal:** Boot ESP-IDF `hello_world` in Renode with correct UART output matching real ESP32-C3 hardware.

**Architecture:** Three-level loop (outer boot-progress loop, middle per-peripheral loop, inner fix-until-pass ralph-loop), with sub-agent supervision at every stage. No human input required.

**Key principle:** Every peripheral gets **dedicated test firmware** that exercises its specific functionality, serving as the ground-truth comparison baseline between hardware, QEMU, and Renode. And every peripheral gets its **own living execution document** tracking plan, progress, and verification -- not one monolithic file.

---

## Context

The repository at `/home/tim/github/mithro/renode-espemu/` contains 35 research documents (8,720 lines) covering all ESP32 peripherals, their register maps, QEMU reference implementations, and Renode implementation guidance. No implementation code exists yet. This plan turns that documentation into working Renode emulation, starting with the ESP32-C3 (RISC-V, whose CPU core already works in Renode) and targeting the 11 boot-critical peripherals needed for `hello_world`.

---

## Document Structure: Per-Peripheral Execution Docs

Each peripheral gets its own execution document at `peripherals/<peripheral>/execution.md`. This is a **living document** updated throughout development. It replaces a monolithic plan by giving each peripheral its own self-contained tracking file.

### Template: `peripherals/<peripheral>/execution.md`

```markdown
# <Peripheral Name> -- Execution Tracker

## Status
| Phase              | Status      | Date       |
|--------------------|-------------|------------|
| Planning           | not started |            |
| Test firmware      | not started |            |
| Hardware baseline  | not started |            |
| QEMU baseline      | not started |            |
| Renode impl        | not started |            |
| Robot test         | not started |            |
| Code review        | not started |            |
| Merged             | not started |            |

## Verification Levels
| Level | Check                        | Status | Evidence |
|-------|------------------------------|--------|----------|
| L1    | C# compiles                  |        |          |
| L2    | Renode loads without crash    |        |          |
| L3    | Boot progress >= previous     |        |          |
| L4    | UART matches hardware baseline|        |          |
| L5    | Register trace matches QEMU   |        |          |

## Branch
- Worktree: (path)
- Branch: peripheral/<name>
- Commits: (list, updated after each commit)

## Planning Notes
(Filled during Step 1 -- which registers to implement, what to stub, etc.)

## Test Firmware Design
(Filled during Step 2 -- what the firmware tests, expected outputs)

## Hardware Baseline
(Filled during Step 3 -- captured output, notable register values)

## Implementation Log
(Filled during Step 4 -- ralph-loop iteration notes, what failed, what was fixed)

## Review Notes
(Filled during Step 7 -- spec compliance findings, code quality findings)

## Issues
(Any problems encountered, workarounds, things to revisit)
```

This document is committed after EVERY update. It serves as the authoritative record for this peripheral.

---

## Test Firmware: The Core of Each Peripheral Loop

Test firmware is the **most important deliverable per peripheral** -- it defines what "correct" looks like. Each peripheral gets a dedicated ESP-IDF application that systematically exercises the hardware.

### Test Firmware Design Principles

1. **One peripheral per firmware** -- isolates behaviour, makes comparison clean
2. **Register-level testing** -- directly read/write peripheral registers (not just use ESP-IDF APIs)
3. **Print everything** -- every register read printed to UART with address and value
4. **Structured output** -- machine-parseable format for automated comparison:
   ```
   [PERIPH] REG_READ  addr=0x60008000 val=0x00000042
   [PERIPH] REG_WRITE addr=0x60008004 val=0x00000001
   [PERIPH] REG_READ  addr=0x60008004 val=0x00000001
   [PERIPH] TEST_PASS feature_name
   [PERIPH] TEST_FAIL feature_name expected=0x42 got=0x00
   ```
5. **Feature-by-feature testing** -- each test firmware exercises specific peripheral capabilities:
   - Reset values: read all registers before any configuration
   - Configuration: write config registers, read back to verify
   - Operation: trigger the peripheral, observe results
   - Interrupts: if applicable, trigger interrupt, verify status register
   - Edge cases: overflow, boundary values, error conditions

### Test Firmware Structure Per Peripheral

```
peripherals/<peripheral>/firmware/
├── CMakeLists.txt          -- ESP-IDF project
├── main/
│   ├── CMakeLists.txt
│   └── test_<peripheral>.c -- Test application
├── sdkconfig.defaults      -- Minimal config for ESP32-C3
└── README.md               -- What this firmware tests and expected output
```

### Example: eFuse Test Firmware

```c
// test_efuse.c -- Read all eFuse blocks, print values
#include <stdio.h>
#include "soc/efuse_reg.h"

void app_main(void) {
    printf("[EFUSE] === Reset Values (BLK0) ===\n");
    for (int i = 0; i < 7; i++) {
        uint32_t addr = EFUSE_BLK0_RDATA0_REG + (i * 4);
        uint32_t val = REG_READ(addr);
        printf("[EFUSE] REG_READ  addr=0x%08x val=0x%08x\n", addr, val);
    }

    // Read MAC address specifically
    uint32_t mac_lo = REG_READ(EFUSE_BLK0_RDATA1_REG);
    uint32_t mac_hi = REG_READ(EFUSE_BLK0_RDATA2_REG);
    printf("[EFUSE] MAC_LO=0x%08x MAC_HI=0x%08x\n", mac_lo, mac_hi);

    // Read chip revision
    uint32_t rd3 = REG_READ(EFUSE_BLK0_RDATA3_REG);
    printf("[EFUSE] CHIP_VER_REG=0x%08x\n", rd3);

    printf("[EFUSE] TEST_PASS read_blk0\n");
    printf("[EFUSE] === Tests Complete ===\n");
}
```

### Example: Timer Group Test Firmware

```c
// test_timer.c -- Configure timer, count, check alarm
#include <stdio.h>
#include "soc/timer_group_reg.h"

void app_main(void) {
    printf("[TIMER] === Reset Values ===\n");
    printf("[TIMER] REG_READ  addr=0x%08x val=0x%08x\n",
           TIMG_T0CONFIG_REG(0), REG_READ(TIMG_T0CONFIG_REG(0)));

    // Enable timer, set divider
    REG_WRITE(TIMG_T0CONFIG_REG(0), 0x60000002); // enable, divider=2
    printf("[TIMER] REG_WRITE addr=0x%08x val=0x60000002\n", TIMG_T0CONFIG_REG(0));

    // Read back config
    uint32_t cfg = REG_READ(TIMG_T0CONFIG_REG(0));
    printf("[TIMER] REG_READ  addr=0x%08x val=0x%08x\n", TIMG_T0CONFIG_REG(0), cfg);

    // Latch and read counter value
    REG_WRITE(TIMG_T0UPDATE_REG(0), 1);
    uint32_t lo = REG_READ(TIMG_T0LO_REG(0));
    uint32_t hi = REG_READ(TIMG_T0HI_REG(0));
    printf("[TIMER] COUNTER lo=0x%08x hi=0x%08x\n", lo, hi);

    if (lo > 0 || hi > 0) {
        printf("[TIMER] TEST_PASS counter_increments\n");
    } else {
        printf("[TIMER] TEST_FAIL counter_increments expected=nonzero got=0\n");
    }

    printf("[TIMER] === Tests Complete ===\n");
}
```

### Baseline Capture Workflow

For each test firmware, capture output on three platforms:

```
peripherals/<peripheral>/baselines/
├── hardware.log        -- Real ESP32-C3 via /dev/ttyESP32C3
├── qemu.log            -- Espressif QEMU (if peripheral supported)
├── renode.log          -- Current Renode output (updated each iteration)
└── comparison.diff     -- Diff between hardware and renode
```

The `comparison.diff` is the primary metric: when it's empty, the peripheral matches hardware.

---

## Three-Level Loop Architecture

```
OUTER LOOP (Boot Progress) -- ralph-loop, max 50 iterations
│  Runs hello_world in Renode, measures boot progress (0-5)
│  Identifies next blocking peripheral from unhandled register accesses
│  Completion promise: UART output contains "Hello world!"
│
├── MIDDLE LOOP (Per-Peripheral) -- 8 steps, own execution doc
│   │  Plan → test firmware → baselines → implement → test → review → merge
│   │  Own execution doc at peripherals/<peripheral>/execution.md
│   │  Own git worktree and branch
│   │
│   └── INNER LOOP (Fix-Until-Pass) -- ralph-loop, max 15 iterations
│       Keep fixing C# model until comparison.diff is empty
│       Tests run on each iteration: compile → smoke → progress → UART match
│       Completion promise: "UART output matches hardware baseline"
│
└── (repeat until boot score 5)
```

---

## Phase 0: Infrastructure Setup (One-Time)

### Task 0.1: Create ESP32-C3 Platform Definition

- [ ] Create `platforms/cpus/esp32c3.repl` with RV32IMC CPU, memory map, ESP32_UART
- [ ] Create `scripts/single-node/esp32c3.resc` boot script
- [ ] Create Python catch-all stub for unmapped peripheral ranges
- [ ] Verify: Renode loads platform without crash

### Task 0.2: Build hello_world and Capture Baselines

- [ ] SSH to rpi4-esp, build ESP-IDF hello_world for ESP32-C3
- [ ] Flash, capture hardware UART → `hello_world/baselines/hardware.log`
- [ ] Capture QEMU output → `hello_world/baselines/qemu.log`
- [ ] Copy firmware binary → `hello_world/firmware/esp32c3_hello_world.bin`

### Task 0.3: Create Tooling

- [ ] Create `tools/measure_boot_progress.py` (scores 0-5, reports first unhandled access)
- [ ] Create `tools/compare_output.py` (diffs structured `[PERIPH]` lines between logs)
- [ ] Create `tools/capture_baseline.py` (automates SSH flash + UART capture on rpi4-esp)
- [ ] Verify: tools run and produce output

### Task 0.4: Initialize Execution Docs and Work Log

- [ ] Create `peripherals/` directory
- [ ] Create initial execution doc for first peripheral (eFuse)
- [ ] Create `docs/work-log.md` with dashboard and empty peripheral status table
- [ ] Commit all

### Task 0.5: Git Branching via Worktrees

- [ ] **Skill:** `using-git-worktrees`
- [ ] Branch convention: `peripheral/<name>` per peripheral
- [ ] Max 2 active worktrees

---

## Peripheral Implementation Order

| # | Peripheral | Execution Doc | QEMU Ref | Est. Days |
|---|---|---|---|---|
| 1 | eFuse | `peripherals/efuse.md` | Yes | 1-2 |
| 2 | RTC Controller | `peripherals/rtc.md` | Yes | 2-3 |
| 3 | DPORT | `peripherals/dport.md` | Yes | 3-5 |
| 4 | Watchdog | `peripherals/watchdog.md` | Yes | 1-2 |
| 5 | Clock Control | `peripherals/clock-control.md` | Partial | 2-3 |
| 6 | Timer Groups | `peripherals/timer-group.md` | Yes | 2-3 |
| 7 | Interrupt Matrix | `peripherals/interrupt-matrix.md` | Yes | 3-5 |
| 8 | Cache/MMU | `peripherals/cache-mmu.md` | Yes | 2-4 wks |
| 9 | SPI Flash | `peripherals/spi.md` | Yes | 3-5 |
| 10 | GPIO | `peripherals/gpio.md` | Yes | 3-5 |
| 11 | RNG | `peripherals/rng.md` | Yes | <1 |

Order adapts dynamically: outer loop identifies the real next blocker from unhandled accesses.

---

## Per-Peripheral Steps (Middle Loop)

### Step 1: Planning

**Skill:** `writing-plans`

Read `docs/peripherals/<name>.md` (existing analysis). Read QEMU reference source. Write planning section of `peripherals/<name>/execution.md`:
- Which registers MUST work for boot (vs can be stubbed)
- C# class structure
- Register offsets, reset values, read/write behaviour
- What the test firmware should exercise

Update execution doc status: Planning → **complete**. Commit.

### Step 2: Write Test Firmware

**Skill:** `test-driven-development`

Create `peripherals/<peripheral>/firmware/` ESP-IDF project. The firmware:
- Reads all registers at reset, prints values (format: `[NAME] REG_READ addr=0xNNN val=0xNNN`)
- Configures peripheral, reads back, prints
- Exercises key features one-by-one, prints PASS/FAIL per feature
- Covers: reset values, basic config, operation, interrupts (if any), edge cases

Build for ESP32-C3. Write `peripherals/<peripheral>/firmware/README.md` documenting what each test covers.

Update execution doc: Test firmware → **complete**. Commit.

### Step 3: Capture Hardware Baseline

SSH to rpi4-esp. Flash test firmware. Capture UART → `peripherals/<peripheral>/baselines/hardware.log`.

If peripheral is QEMU-supported: capture QEMU output → `peripherals/<peripheral>/baselines/qemu.log`.

Update execution doc: Hardware baseline → **complete**. Commit baselines.

### Step 4: Implement in Renode (ralph-loop)

**Skill:** `ralph-loop` + `test-driven-development`

```
/ralph-loop "
1. Read peripherals/<PERIPHERAL>.md for planning notes
2. Read QEMU source for reference
3. Implement/fix peripherals/<PERIPHERAL>/ESP32C3_<PERIPHERAL>.cs
4. Update esp32c3.repl
5. Build (verify compiles)
6. Run test firmware in Renode, capture output
7. Diff against peripherals/<PERIPHERAL>/baselines/hardware.log
8. If diff non-empty: identify discrepancy, fix, commit
9. Run boot progress measurement
10. Update peripherals/<PERIPHERAL>.md with iteration notes
" --completion-promise "UART output matches hardware baseline" --max-iterations 15
```

Each iteration: commit changes, update execution doc implementation log.

### Step 5: Robot Framework Test

Create `peripherals/<peripheral>/test.robot` that:
- Loads platform + firmware
- Waits for specific `[PERIPH] TEST_PASS` lines
- Verifies no `[PERIPH] TEST_FAIL` lines appear
- Checks boot progress doesn't regress

Verify: `renode-test peripherals/<peripheral>/test.robot` passes. Commit.

### Step 6: Boot Progress Re-measurement

Run `tools/measure_boot_progress.py`. Compare to previous score. Record in execution doc and work log. Identify next blocker. Commit.

### Step 7: Code Review (two-stage)

**Stage 1 -- Spec Compliance:** C# vs peripheral doc + QEMU + ESP-IDF headers. All boot-critical registers implemented? Reset values match? Read-only correct?

**Stage 2 -- Code Quality:** Renode patterns, no warnings, proper logging, clean naming.

Record findings in execution doc review section. If issues → ralph-loop fixes → re-review.

### Step 8: Merge

**Skill:** `finishing-a-development-branch`, `verification-before-completion`

Run ALL robot tests (new + previous). All must pass. Merge to main. Clean worktree. Update execution doc: Merged → **complete**. Update work log dashboard. Commit.

---

## Outer Loop

**Skill:** `ralph-loop`

```
/ralph-loop "
1. Run tools/measure_boot_progress.py
2. Read docs/work-log.md
3. If score=5, STOP
4. Identify next peripheral from first unhandled register
5. Create execution doc from template if not exists
6. Create worktree, execute per-peripheral Steps 1-8
7. Merge, update work log
8. Commit
" --completion-promise "Hello world!" --max-iterations 50
```

---

## Sub-Agent Rules

Max 2 concurrent. Sub-agent A (implementer) + Sub-agent B (tester/reviewer). Never both write to same file/branch.

**Allowed parallel:** A implements peripheral N while B reviews N-1, or A writes firmware while B captures hardware baseline.

---

## File Layout

Everything for a peripheral lives together under `peripherals/<name>/`. The existing `docs/peripherals/<name>.md` analysis docs remain as read-only reference.

```
renode-espemu/
├── platforms/cpus/esp32c3.repl          -- shared platform definition
├── scripts/single-node/esp32c3.resc     -- shared boot script
├── tools/
│   ├── measure_boot_progress.py         -- shared tooling
│   ├── compare_output.py
│   └── capture_baseline.py
├── docs/
│   ├── work-log.md                      -- global dashboard
│   ├── peripherals/                     -- existing analysis docs (reference)
│   └── ...                              -- existing research docs
│
├── peripherals/                         -- ALL per-peripheral output grouped here
│   ├── efuse/
│   │   ├── execution.md                 -- living plan/progress/review tracker
│   │   ├── ESP32C3_eFuse.cs             -- Renode C# peripheral model
│   │   ├── firmware/                    -- ESP-IDF test app
│   │   │   ├── CMakeLists.txt
│   │   │   ├── main/test_efuse.c
│   │   │   └── sdkconfig.defaults
│   │   ├── baselines/
│   │   │   ├── hardware.log             -- real ESP32-C3 output
│   │   │   ├── qemu.log                 -- QEMU output (if supported)
│   │   │   ├── renode.log               -- current Renode output
│   │   │   └── comparison.diff          -- hw vs renode diff
│   │   └── test.robot                   -- Robot Framework regression test
│   │
│   ├── rtc/
│   │   ├── execution.md
│   │   ├── ESP32C3_RTC.cs
│   │   ├── firmware/
│   │   ├── baselines/
│   │   └── test.robot
│   │
│   ├── rng/
│   │   ├── execution.md
│   │   ├── ESP32C3_RNG.cs
│   │   ├── firmware/
│   │   ├── baselines/
│   │   └── test.robot
│   │
│   └── ... (one directory per peripheral)
│
└── hello_world/                         -- the ultimate goal test
    ├── baselines/
    │   ├── hardware.log
    │   └── qemu.log
    ├── firmware/esp32c3_hello_world.bin
    └── test.robot                       -- passes when boot score = 5
```

**Key principle:** `peripherals/efuse/` contains EVERYTHING about eFuse -- the execution tracker, the C# model, the test firmware, the baselines, and the robot test. Finding anything about a peripheral means looking in one place.

---

## Risk Mitigation

| Risk | Mitigation |
|---|---|
| Cache/MMU too complex | Map flash directly into memory (QEMU's approach) |
| Cascading failures | Every ralph-loop iteration runs ALL existing robot tests |
| ralph-loop stuck | Commit WIP, log diagnosis, try next peripheral, revisit later |
| rpi4-esp unavailable | Use QEMU baselines as backup |
| Renode build unfamiliar | Start with RNG (trivial ~30 lines) as learning vehicle |
| Test firmware doesn't capture right thing | Hardware baseline is ground truth; iterate firmware too |

---

## Estimated Timeline

| Phase | Duration | Peripherals | Boot Score |
|---|---|---|---|
| Phase 0: Infrastructure | 1-2 days | 0 | 0→1 |
| Batch 1: eFuse + RNG | 2-3 days | 2 | 1→2 |
| Batch 2: RTC + Watchdog | 3-4 days | 4 | 2→3 |
| Batch 3: DPORT + Clock | 4-5 days | 6 | 3→3 |
| Batch 4: Timer + IntMatrix | 4-6 days | 8 | 3→4 |
| Batch 5: Cache/MMU + SPI | 2-4 weeks | 10 | 4→5 |
| Batch 6: GPIO (if needed) | 2-3 days | 11 | 5 |

---

## Verification: End-to-End

1. **Per-peripheral:** `peripherals/<name>/baselines/comparison.diff` is empty
2. **Per-peripheral robot:** `renode-test peripherals/<name>/test.robot` passes
3. **All peripherals:** `renode-test peripherals/*/test.robot` -- all pass, no regressions
4. **Ultimate:** `renode-test hello_world/test.robot` -- "Hello world!" appears in UART
5. **Hardware match:** `tools/compare_output.py --renode renode.log --hardware hardware.log` -- no diff
