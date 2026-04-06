# ESP32-C3 Peripheral Test Plan

> Goal: Complete, accurate emulation of every ESP32-C3 peripheral, verified by
> focused test firmware that produces identical structured output on real
> hardware and in Renode.

## Principles

1. **One test per logical feature** — not one monolithic test per peripheral
2. **Every register tested** — read at reset, write, read back, verify value
3. **Every behavior tested** — counters count, alarms fire, interrupts deliver
4. **Structured output** — `[TAG] REG_READ addr=0xNNN val=0xNNN` for machine comparison
5. **Hardware is ground truth** — Renode output must match hardware exactly
6. **Zero-diff target** — `compare_output.py` reports no differences

## Test Firmware Convention

Each test is a standalone ESP-IDF app in:
```
peripherals/<peripheral>/firmware/<test_name>/
├── CMakeLists.txt
├── sdkconfig.defaults
└── main/
    ├── CMakeLists.txt
    └── <test_name>.c
```

Output format:
```
[TAG] === <Test Name> ===
[TAG] REG_READ  addr=0x600XXXXX val=0xNNNNNNNN  <register_name>
[TAG] REG_WRITE addr=0x600XXXXX val=0xNNNNNNNN  <register_name>
[TAG] REG_READ  addr=0x600XXXXX val=0xNNNNNNNN  <register_name> (after write)
[TAG] TEST_PASS <feature_name>
[TAG] TEST_FAIL <feature_name> expected=0xNN got=0xNN
[TAG] === Done ===
```

Each test firmware ends with `while(1) { vTaskDelay(1000/portTICK_PERIOD_MS); }`

## Per-Peripheral Test Breakdown

---

### 1. eFuse (0x60008800) — Tag: `[EFUSE]`

**Registers:** 115 in header. Read-only OTP storage + programming + status.

| # | Test Name | What It Tests |
|---|---|---|
| 1.1 | `test_efuse_blk0_reset_values` | Read RD_WR_DIS, RD_REPEAT_DATA0-4 at reset. Print all values. |
| 1.2 | `test_efuse_mac_address` | Read RD_MAC_SPI_SYS_0-5, decode and print 6-byte MAC address. |
| 1.3 | `test_efuse_chip_revision` | Read WAFER_VERSION_MINOR/MAJOR, PKG_VERSION, BLK_VERSION. Decode chip rev. |
| 1.4 | `test_efuse_blk1_spi_config` | Read all RD_MAC_SPI_SYS_0-5 raw values (SPI pad config, flash cap). |
| 1.5 | `test_efuse_blk2_sys_part1` | Read RD_SYS_PART1_DATA0-7 (system parameters). |
| 1.6 | `test_efuse_blk3_usr_data` | Read RD_USR_DATA0-7 (user data block). |
| 1.7 | `test_efuse_key_blocks` | Read RD_KEY0-KEY5 DATA0-7 (key storage blocks 4-9). |
| 1.8 | `test_efuse_control_regs` | Read CLK, CONF, STATUS, CMD, DATE. Write CMD read bit, verify STATUS. |
| 1.9 | `test_efuse_int_regs` | Read INT_RAW, INT_ST, INT_ENA. Write INT_ENA, verify INT_ST masking. |

**C# implementation gaps:** Currently only defines registers 0x2C-0x178 + control. Need all key block registers verified.

---

### 2. RTC Controller (0x60008000) — Tag: `[RTC]`

**Registers:** 76 in header. Reset state, timers, clocks, brownout, WDT, power.

| # | Test Name | What It Tests |
|---|---|---|
| 2.1 | `test_rtc_reset_state` | Read RESET_STATE_REG, decode reset cause for CPU. |
| 2.2 | `test_rtc_time_counter` | Write TIME_UPDATE (latch), read TIME_LOW0/HIGH0. Latch again, verify advances. Verify HIGH/LOW are consistent (same latch). |
| 2.3 | `test_rtc_store_registers` | Write unique values to STORE0-7, read back, verify. Test STORE4 (XTAL_FREQ) encoding. |
| 2.4 | `test_rtc_clk_conf` | Read CLK_CONF, SLOW_CLK_CONF reset values. Write CLK_CONF, read back. |
| 2.5 | `test_rtc_brownout` | Read BROWN_OUT_REG reset value. Verify enable bit, detect bit. |
| 2.6 | `test_rtc_wdt_config` | Read WDTCONFIG0-4 reset values. Write WDTWPROTECT unlock key (0x50D83AA1), write config, read back. |
| 2.7 | `test_rtc_int_registers` | Read INT_ENA, INT_RAW, INT_ST, write INT_CLR. Verify masking (ST = RAW & ENA). |
| 2.8 | `test_rtc_options_state` | Read OPTIONS0, STATE0, TIMER1-6, ANA_CONF reset values. |
| 2.9 | `test_rtc_power_regs` | Read BIAS_CONF, REG, PWC, DIG_PWC, DIG_ISO reset values. |
| 2.10 | `test_rtc_swd` | Read SWD_CONF, SWD_WPROTECT. Write unlock, configure, read back. |

**C# implementation gaps:** Many registers between 0xD8 and 0x10C not defined (only FIB_SEL at 0x10C and SENSOR_CTRL at 0x11C added). Need DIG_PAD_HOLD, PAD_HOLD, etc.

---

### 3. Interrupt Matrix (0x600C2000) — Tag: `[INTMTX]`

**Registers:** 104 in header. Source mapping, enable, type, priority, threshold.

| # | Test Name | What It Tests |
|---|---|---|
| 3.1 | `test_intmtx_reset_values` | Read all 64 source mapping registers at reset (should be 0). Read CPU_INT_ENABLE, TYPE, EIP_STATUS, THRESH. |
| 3.2 | `test_intmtx_source_mapping` | Write source N to line M, read back, verify. Test all 5 mapping bits. |
| 3.3 | `test_intmtx_enable_mask` | Write CPU_INT_ENABLE, read back. Enable/disable individual lines. |
| 3.4 | `test_intmtx_priority` | Write CPU_INT_PRI_0-31, read back. Verify 4-bit values. |
| 3.5 | `test_intmtx_threshold` | Write CPU_INT_THRESH, read back. Verify priority filtering. |
| 3.6 | `test_intmtx_type_edge_level` | Write CPU_INT_TYPE (edge/level per line), read back. |
| 3.7 | `test_intmtx_pending_clear` | Read CPU_INT_EIP_STATUS. Write CPU_INT_CLEAR, verify pending clears. |
| 3.8 | `test_intmtx_firmware_config` | Read the source mappings, enable, and priorities as configured by ESP-IDF boot (captures the firmware's actual interrupt setup). |

**C# implementation gaps:** All registers defined but some behaviors may differ (e.g., EIP_STATUS should reflect source levels ANDed with enable, not just manual trigger state).

---

### 4. SYSTIMER (0x60023000) — Tag: `[SYSTMR]`

**Registers:** 30 in header. Counters, alarms, interrupt.

| # | Test Name | What It Tests |
|---|---|---|
| 4.1 | `test_systimer_conf_reset` | Read CONF register at reset. Verify CLK_EN, UNIT0/1_WORK_EN, TARGET0/1/2_WORK_EN defaults. |
| 4.2 | `test_systimer_counter_latch` | Write UNIT0_OP to latch. Read VALUE_HI/LO. Verify VALUE_VALID bit. Latch again, verify counter advanced. |
| 4.3 | `test_systimer_counter_load` | Write UNIT0_LOAD_HI/LO with known value. Write UNIT_LOAD to load. Latch and read back. Verify loaded value. |
| 4.4 | `test_systimer_target_oneshot` | Configure TARGET0 with a future value. Write COMP_LOAD. Wait. Read INT_RAW to see if alarm fired. Clear via INT_CLR. |
| 4.5 | `test_systimer_target_periodic` | Configure TARGET0 in period mode (CONF bit 30). Set period. Write COMP_LOAD. Wait for multiple fires. Read INT_RAW between clears. |
| 4.6 | `test_systimer_target_conf` | Write TARGET0/1/2_CONF (period mode, unit select). Read back. Verify all bits. |
| 4.7 | `test_systimer_int_registers` | Write INT_ENA, read back. Read INT_RAW, INT_ST (verify masking). Write INT_CLR, verify clear. |
| 4.8 | `test_systimer_unit1` | Repeat counter latch/load tests for UNIT1. Verify independence from UNIT0. |
| 4.9 | `test_systimer_date` | Read DATE register, verify non-zero. |

**C# implementation gaps:** VALUE_VALID never cleared. Counter doesn't use actual elapsed time. Target enable bits not checked in UpdateTimerEnabled. TimerTickInterval type mismatch.

---

### 5. Timer Group (0x6001F000 / 0x60020000) — Tag: `[TIMG]`

**Registers:** ~254 defines in header. Timer 0, WDT, RTC calibration.

| # | Test Name | What It Tests |
|---|---|---|
| 5.1 | `test_timg_t0_config_reset` | Read T0CONFIG reset value. Verify INCREASE, AUTORELOAD, DIVIDER defaults. |
| 5.2 | `test_timg_t0_counter` | Enable timer. Write T0UPDATE to latch. Read T0LO/T0HI. Latch again, verify counter advanced. |
| 5.3 | `test_timg_t0_divider` | Set different divider values. Enable timer. Measure counter rate relative to divider. |
| 5.4 | `test_timg_t0_alarm` | Set alarm value. Enable alarm. Start timer. Wait for INT_RAW T0 bit. Verify alarm fires at correct count. |
| 5.5 | `test_timg_t0_reload` | Set LOADLO/LOADHI. Write T0LOAD to reload counter. Latch and verify reloaded value. |
| 5.6 | `test_timg_rtc_calibration` | Read RTCCALICFG (verify RDY bit). Read RTCCALICFG1 (calibration value). Verify RTCCALICFG2 (no timeout). |
| 5.7 | `test_timg_wdt_config` | Write WDTWPROTECT unlock key. Write WDTCONFIG0-4. Read back. Verify write-protect works (wrong key → writes ignored). |
| 5.8 | `test_timg_int_registers` | Write INT_ENA, read back. Read INT_RAW, INT_ST. Write INT_CLR. Verify masking and clear. |
| 5.9 | `test_timg_regclk_date` | Read REGCLK and NTIMERS_DATE registers. |
| 5.10 | `test_timg1_independence` | Repeat key tests on TIMG1 (0x60020000). Verify independent from TIMG0. |

**C# implementation gaps:** timerDivider stored but not used. AdvanceTimer always adds 1000 regardless of divider.

---

### 6. SYSTEM (0x600C0000) — Tag: `[SYSTEM]`

**Registers:** 40 in header. Clock gating, reset, cross-core interrupt.

| # | Test Name | What It Tests |
|---|---|---|
| 6.1 | `test_system_clk_en_reset` | Read PERIP_CLK_EN0, PERIP_CLK_EN1, CPU_PERI_CLK_EN reset values. |
| 6.2 | `test_system_rst_en_reset` | Read PERIP_RST_EN0, PERIP_RST_EN1, CPU_PERI_RST_EN reset values. |
| 6.3 | `test_system_clk_write_readback` | Write PERIP_CLK_EN0 (toggle bits), read back. Verify read-modify-write works. |
| 6.4 | `test_system_cpu_intr` | Write 1 to CPU_INTR_FROM_CPU_0, read back (should be 0 — auto-clear). Write 0, read back. |
| 6.5 | `test_system_sysclk_conf` | Read SYSCLK_CONF. Verify CLK_XTAL_FREQ (RO, should be 40). Verify SOC_CLK_SEL, PRE_DIV_CNT. |
| 6.6 | `test_system_cache_control` | Read CACHE_CONTROL_REG reset value. |
| 6.7 | `test_system_bt_lpck` | Read BT_LPCK_DIV_INT, BT_LPCK_DIV_FRAC reset values. |
| 6.8 | `test_system_date` | Read SYSTEM_DATE_REG. |

**C# implementation gaps:** CLK_XTAL_FREQ is R/W but should be RO.

---

### 7. RNG / SYSCON (0x60026000) — Tag: `[SYSCON]`

**Registers:** 43 in header. WiFi/BT clocks, memory power, RNG.

| # | Test Name | What It Tests |
|---|---|---|
| 7.1 | `test_syscon_wifi_clk_reset` | Read WIFI_CLK_EN reset value (should be 0xFFFFFFFF). Read WIFI_RST_EN. |
| 7.2 | `test_syscon_wifi_clk_write` | Write WIFI_CLK_EN (toggle bits), read back. Verify read-modify-write. |
| 7.3 | `test_syscon_rng_reads` | Read RND_DATA 20 times. Verify all non-zero, at least 15 distinct values. |
| 7.4 | `test_syscon_mem_power` | Read CLKGATE_FORCE_ON, MEM_POWER_DOWN, MEM_POWER_UP reset values. Write and read back. |
| 7.5 | `test_syscon_front_end` | Read FRONT_END_MEM_PD, FRONT_END_MEM_PD_2 reset values. |
| 7.6 | `test_syscon_date` | Read SYSCON_DATE_REG. |

**C# implementation gaps:** Many SYSCON registers have placeholder offsets (ReservedReg38 etc.) — need proper names from header.

---

### 8. GPIO (0x60004000) — Tag: `[GPIO]`

**Registers:** 199 in header. Output, enable, input, status, per-pin config, mux.

| # | Test Name | What It Tests |
|---|---|---|
| 8.1 | `test_gpio_out_reset` | Read OUT_REG, ENABLE_REG, STATUS_REG at reset (all should be 0). |
| 8.2 | `test_gpio_out_w1ts_w1tc` | Write OUT_W1TS to set bits. Read OUT_REG. Write OUT_W1TC to clear. Read OUT_REG. Verify W1TS/W1TC reads return 0. |
| 8.3 | `test_gpio_enable_w1ts_w1tc` | Same pattern for ENABLE_W1TS/W1TC. |
| 8.4 | `test_gpio_status_w1ts_w1tc` | Same pattern for STATUS_W1TS/W1TC. |
| 8.5 | `test_gpio_in_loopback` | Set OUT and ENABLE for a pin. Read IN_REG. Verify loopback (output drives input). |
| 8.6 | `test_gpio_pin_config` | Write PIN0-PIN21 config registers (int type, pad driver, wakeup). Read back each. |
| 8.7 | `test_gpio_func_out_sel` | Read FUNC_OUT_SEL_CFG for pins 0-21 (default 0x80). Write and read back. |
| 8.8 | `test_gpio_func_in_sel` | Read FUNC_IN_SEL_CFG for selected input functions. Write and read back. |
| 8.9 | `test_gpio_strap` | Read GPIO_STRAP_REG (boot strapping pins). |
| 8.10 | `test_gpio_clock_date` | Read CLOCK_GATE_REG, DATE_REG. |

**C# implementation gaps:** Field width 26 vs PinMask 22 inconsistency. STRAP register returns 0 (should reflect boot config).

---

### 9. ExtMem / Cache (0x600C4000) — Tag: `[EXTMEM]`

**Registers:** 66 in header. ICache control, sync, preload, autoload, MMU fault.

| # | Test Name | What It Tests |
|---|---|---|
| 9.1 | `test_extmem_icache_ctrl` | Read ICACHE_CTRL (bit 0 = enable). Read ICACHE_CTRL1 reset value. |
| 9.2 | `test_extmem_sync` | Read ICACHE_SYNC_CTRL (SYNC_DONE bit). Write SYNC_ADDR, SYNC_SIZE, read back. |
| 9.3 | `test_extmem_preload` | Read ICACHE_PRELOAD_CTRL (PRELOAD_DONE bit). Write PRELOAD_ADDR, SIZE, read back. |
| 9.4 | `test_extmem_autoload` | Read ICACHE_AUTOLOAD_CTRL (AUTOLOAD_DONE bit). Read SCT0/SCT1 addr/size. |
| 9.5 | `test_extmem_tag_power` | Read ICACHE_TAG_POWER_CTRL reset value. |
| 9.6 | `test_extmem_cache_state` | Read CACHE_STATE_REG (should be idle = 0x001). |
| 9.7 | `test_extmem_mmu_fault` | Read CACHE_MMU_FAULT_CONTENT, CACHE_MMU_FAULT_VADDR. Verify FAULT_CODE field position. |
| 9.8 | `test_extmem_freeze` | Read ICACHE_FREEZE (FREEZE_DONE bit). |
| 9.9 | `test_extmem_pms` | Read ICACHE_PMS boundary and lock registers. |
| 9.10 | `test_extmem_conf_date` | Read CACHE_CONF_MISC, DATE_REG. |

**C# implementation gaps:** SYNC_CTRL default wrong (0x3 vs 0x1). CACHE_MMU_FAULT_CODE field was at wrong bit position (fixed but needs verification). Autoload size field widths uncertain.

---

### 10. UART (0x60000000) — Tag: `[UART]`

**Registers:** ~765 defines in header. FIFO, config, flow control, baud detect, interrupts.

| # | Test Name | What It Tests |
|---|---|---|
| 10.1 | `test_uart_status_reset` | Read STATUS_REG (TXFIFO_CNT=0, RXFIFO_CNT=0). Read CONF0, CONF1 reset values. |
| 10.2 | `test_uart_tx_fifo` | Write bytes to FIFO_REG. Read STATUS (TXFIFO_CNT should reflect). Verify bytes appear on output. |
| 10.3 | `test_uart_conf_readback` | Write CONF0 (data bits, stop bits, parity). Read back. Write CONF1 (FIFO thresholds). Read back. |
| 10.4 | `test_uart_clk_conf` | Read CLK_CONF reset value. Write CLK_CONF, read back. |
| 10.5 | `test_uart_clkdiv` | Read CLKDIV_REG reset value. Write divider, read back. |
| 10.6 | `test_uart_int_registers` | Write INT_ENA, read back. Read INT_RAW, INT_ST (verify masking). Write INT_CLR. |
| 10.7 | `test_uart_flow_idle_sleep` | Read FLOW_CONF, SLEEP_CONF, IDLE_CONF reset values. Write and read back. |
| 10.8 | `test_uart_mem_conf` | Read MEM_CONF reset value. Write and read back. |
| 10.9 | `test_uart_fsm_baud` | Read FSM_STATUS (idle). Read LOWPULSE, HIGHPULSE, POSPULSE, NEGPULSE. |
| 10.10 | `test_uart_date_id` | Read DATE_REG, ID_REG. |

**C# implementation gaps:** IdleConf was at wrong offset (fixed). Missing many registers between defined ones. STATUS bit layout needs verification.

---

### 11. IO MUX (0x60009000) — Tag: `[IOMUX]` — NOT YET IMPLEMENTED

**Registers:** Per-pin mux registers. Currently Python stub.

| # | Test Name | What It Tests |
|---|---|---|
| 11.1 | `test_iomux_pin_reset_values` | Read all 22 GPIO_PINn_MUX_SEL registers at reset. |
| 11.2 | `test_iomux_pin_config` | Write function select, pull-up/down, drive strength per pin. Read back. |
| 11.3 | `test_iomux_date` | Read DATE_REG. |

**C# implementation needed:** Full peripheral from scratch.

---

### 12. SPI Flash Controller (0x60002000) — Tag: `[SPIMEM]` — NOT YET IMPLEMENTED

**Registers:** ~723 defines in spi_mem_reg.h. Complex SPI protocol controller.

| # | Test Name | What It Tests |
|---|---|---|
| 12.1 | `test_spimem_ctrl_reset` | Read SPI_MEM_CTRL_REG, CMD_REG, ADDR_REG reset values. |
| 12.2 | `test_spimem_user_conf` | Read SPI_MEM_USER_REG, USER1_REG, USER2_REG reset values. |
| 12.3 | `test_spimem_timing` | Read timing calibration registers. |
| 12.4 | `test_spimem_cache_ctrl` | Read CACHE_FCTRL, CACHE_SCTRL registers. |
| 12.5 | `test_spimem_flash_id` | Trigger RDID command. Read flash manufacturer/device ID. |
| 12.6 | `test_spimem_status` | Read SPI_MEM_FSM_REG, SPI_MEM_INT_RAW, SPI_MEM_INT_ST. |

**C# implementation needed:** Minimum viable: chip ID response + status register for init_flash to succeed.

---

### 13-29. Remaining Stub Peripherals

Each gets at minimum a `test_<name>_reset_values` that reads all registers at reset. These establish the baseline even before C# implementation.

| # | Peripheral | Address | Priority | Test Count |
|---|---|---|---|---|
| 13 | SPI0 | 0x60003000 | Low | 1 (reset values) |
| 14 | FE2 (RF Frontend) | 0x60005000 | Low | 1 |
| 15 | FE (RF Frontend) | 0x60006000 | Low | 1 |
| 16 | RTC I2C | 0x6000E000 | Low | 1 |
| 17 | NRX | 0x6001C000 | Low | 1 |
| 18 | BB (Baseband) | 0x6001E000 | Low | 1 |
| 19 | UART1 | 0x60010000 | Medium | 3 (reuse UART tests) |
| 20 | I2C | 0x60013000 | Medium | 2 |
| 21 | UHCI0 | 0x60014000 | Low | 1 |
| 22 | RMT | 0x60016000 | Medium | 2 |
| 23 | LEDC | 0x60019000 | Medium | 3 |
| 24 | SPI2 (GP-SPI) | 0x60024000 | Medium | 2 |
| 25 | TWAI | 0x6002B000 | Medium | 2 |
| 26 | I2S | 0x6002D000 | Low | 1 |
| 27 | AES | 0x6003A000 | Medium | 2 |
| 28 | SHA | 0x6003B000 | Medium | 2 |
| 29 | RSA | 0x6003C000 | Low | 1 |
| 30 | Digital Signature | 0x6003D000 | Low | 1 |
| 31 | HMAC | 0x6003E000 | Low | 1 |
| 32 | GDMA | 0x6003F000 | Medium | 3 |
| 33 | APB SAR ADC | 0x60040000 | Medium | 2 |
| 34 | USB Serial/JTAG | 0x60043000 | Medium | 2 |
| 35 | Sensitive (PMS) | 0x600C1000 | Medium | 2 |
| 36 | MMU Table | 0x600C5000 | Low | 1 |
| 37 | XTS-AES | 0x600CC000 | Low | 1 |
| 38 | Assist Debug | 0x600CE000 | Low | 1 |
| 39 | World Controller | 0x600D0000 | Low | 1 |

---

## Test Counts Summary

| Category | Peripherals | Tests |
|---|---|---|
| Implemented C# (complete tests) | 10 | 93 |
| IO MUX (new C# needed) | 1 | 3 |
| SPI Flash (new C#, complex) | 1 | 6 |
| Remaining stubs (reset values) | 22 | ~35 |
| **Total** | **34** | **~137** |

## Infrastructure Needed

| # | Item | Description |
|---|---|---|
| I.1 | Multi-firmware build script | Extend `build_all_firmware.py` to handle multiple firmware per peripheral (directories under `firmware/`). |
| I.2 | Renode baseline capture | Run each firmware in Renode, save UART output to `baselines/renode/<test_name>.log`. |
| I.3 | Hardware baseline capture | Flash each firmware to rpi4-esp, save UART output to `baselines/hardware/<test_name>.log`. |
| I.4 | Per-test comparison | Compare each test's hardware vs Renode output. Report per-register diffs. |
| I.5 | Per-test Robot tests | One Robot test case per firmware. Verify structured output appears. |
| I.6 | CI integration | `python3 tools/ci.py` runs all ~137 firmware, compares all, reports zero-diff. |

## Next Steps

**Improve register accuracy** — fix remaining HW vs Renode mismatches
in existing C# peripherals (49/81 match currently).

**New C# peripherals** — promote high-impact Python placeholders to
full C# implementations with test firmware and HW baselines.

**Zero-diff verification** — run all tests in both Renode and on
real hardware, fix every discrepancy.
