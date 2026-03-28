# ESP32 Random Number Generator

## Overview

The ESP32 includes a hardware Random Number Generator (RNG) that produces 32-bit random values. The RNG is part of the hardware cryptography accelerator block and is accessed through a single register read. When the WiFi or Bluetooth subsystem is enabled and operating, the RNG produces true random numbers seeded by RF noise from the analog subsystem. When WiFi/BT are not active, the output is pseudo-random based on internal oscillator sampling, and the quality of randomness is significantly reduced.

The RNG is the simplest peripheral on the ESP32 from an emulation standpoint. It consists of a single read-only register that returns a new 32-bit random value on each read. There is no configuration, no interrupts, no DMA, and no state machine. The primary concern for emulation is simply returning plausible random values to firmware that reads the register.

ESP-IDF uses the RNG during boot for stack canary initialization and during runtime for various cryptographic operations, random delay insertions, and as a seed source for software PRNGs. The `esp_random()` API and the `bootloader_random_enable()`/`bootloader_random_disable()` functions access this register. It is also used by the mbedTLS integration for cryptographic key generation and nonce creation.

## Hardware Specifications

- **Register base address:** `0x3FF75144` (WDEV base + 0x144, within the WiFi peripheral region)
  - Also accessible via the hardware crypto block as part of `DR_REG_HWCRYPTO_BASE`
- **Number of instances:** 1
- **Key capabilities:**
  - 32-bit random value per read
  - True random number generation when WiFi/BT RF subsystem is active (thermal noise source)
  - Pseudo-random when RF subsystem is inactive (internal RC oscillator jitter)
  - No configuration required -- always produces a value on read
- **Interrupt sources:** None
- **DMA support:** None

## TRM Chapter Reference

- **ESP32 Technical Reference Manual** Chapter 24: Random Number Generator
  - [PDF link](https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf)

## Register Map Summary

The RNG has an extremely simple register interface:

| Address | Register | Purpose |
|---------|----------|---------|
| `0x3FF75144` | `WDEV_RND_REG` | Read-only: returns a 32-bit random value on each read |

That is the complete register map. There are no control registers, no status registers, and no configuration registers. The RNG is always active and always produces a value when read.

Note: Some documentation references the RNG through the hardware cryptography base address (`DR_REG_HWCRYPTO_BASE` = `0x3FF04000`). The register at `0x3FF75144` is the actual hardware RNG output. The hwcrypto block may have its own interface to this value.

## Source Code References

### ESP-IDF Register Definitions
- [`soc/hwcrypto_reg.h`](https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/include/soc/hwcrypto_reg.h) -- Contains `DR_REG_HWCRYPTO_BASE` and related crypto block definitions. The RNG register is defined as part of the WDEV region.

### ESP-IDF HAL (Low-Level Driver)

The RNG does not have a dedicated HAL LL file due to its simplicity. Access is through direct register reads:
- `esp_random()` in `esp_hw_support/hw_random.c` reads the WDEV_RND_REG register directly
- `bootloader_random_enable()` / `bootloader_random_disable()` configure SAR ADC and I2S clocks to provide entropy during early boot (before WiFi is started)

### ESP-IDF API Documentation

The RNG is documented as part of the System API:
- ESP-IDF provides `esp_random()` which reads the hardware RNG register and returns the value
- `esp_fill_random()` fills a buffer with random bytes using repeated reads from the RNG register

### ESP-IDF Examples

There are no dedicated RNG examples in ESP-IDF since usage is trivial (`uint32_t val = esp_random()`). The RNG is used implicitly by:
- All TLS/SSL connections (via mbedTLS entropy source)
- WiFi WPA key generation
- `esp_random()` calls in application code

### Espressif QEMU Implementation
- [`hw/misc/esp32_rng.c`](https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_rng.c) -- QEMU's RNG model

QEMU's implementation is very simple: it registers a memory-mapped region and returns a random value (from the host's PRNG) on each read of the RNG register. There is no modeling of the WiFi/BT entropy source quality difference. The implementation is approximately 50-60 lines of code. It uses `qemu_guest_random_seed_thread_part2()` or similar host randomness API to generate values.

## Renode Implementation Analysis

### Existing Renode Model

No ESP32-specific RNG model exists in Renode. The platform may not currently map this register region.

### Recommended Renode Reference Peripherals
- [`NRF52840_RNG.cs`](https://github.com/renode/renode-infrastructure/blob/master/src/Emulator/Peripherals/Peripherals/Miscellaneous/NRF52840_RNG.cs) -- Nordic's RNG is a good reference even though it is more complex than needed. The nRF52840 RNG has start/stop control, an event-driven interface, and interrupt support. The ESP32 RNG is simpler (just a register read), but the NRF52840_RNG.cs shows how Renode handles random number generation (using `EmulationManager.Instance.CurrentEmulation.RandomGenerator`).

### Implementation Approach

This is the simplest possible Renode peripheral to implement:

**Minimum viable implementation (approximately 30-50 lines of C#):**
1. Create a class inheriting from `BasicDoubleWordPeripheral` (or `IDoubleWordPeripheral`)
2. Register a single read handler at offset 0 (relative to base address `0x3FF75144`)
3. On each read, return a random 32-bit value using Renode's built-in random number generator
4. Writes to the register should be ignored (no-op)

**Registers that MUST be implemented:**
- `WDEV_RND_REG` at the mapped base address -- must return a new random value on every read

**Registers that can be stubbed:**
- None -- there is only one register

**Interrupts that need to work:**
- None -- the RNG has no interrupts

**DMA considerations:**
- None -- the RNG has no DMA

**Pseudo-code for the implementation:**
```
class ESP32_RNG : BasicDoubleWordPeripheral
    Register at offset 0x0:
        Read: return pseudoRandomGenerator.Next() (32-bit)
        Write: ignore
```

**Estimated complexity:** Simple (single register, no state, no interrupts)

### Key Firmware Interactions

**During boot (ROM bootloader):**
1. ROM bootloader calls `bootloader_random_enable()` which configures SAR ADC clocks to feed entropy into the RNG
2. ROM reads `WDEV_RND_REG` to generate stack canary value
3. `bootloader_random_disable()` is called before starting the main application

**During ESP-IDF startup:**
1. Stack canary is initialized using `esp_random()`
2. Various subsystem initializations may call `esp_random()` for seeding

**During application runtime:**
1. `esp_random()` and `esp_fill_random()` read the register for application randomness
2. mbedTLS entropy source reads the register for cryptographic operations
3. WiFi stack reads the register for WPA nonces and key generation

**Critical register accesses that MUST succeed:**
- Any read of `WDEV_RND_REG` must return a value (not fault). Returning the same value repeatedly is acceptable for basic bring-up but may cause issues with cryptographic operations that check for entropy quality.
- Returning 0 on every read would be problematic (stack canary = 0 may trigger assertions, and some firmware checks for "stuck" RNG output).

## Complexity Assessment

- **Estimated difficulty:** Simple
- **Estimated register count:** 1 register
- **Dependencies:** None (the RNG is standalone; WiFi/BT entropy source quality is not relevant for emulation)
- **Priority:** Important for apps -- firmware reads this during boot for stack canary initialization and during runtime for cryptographic operations. A missing or faulting RNG register will cause boot failures. However, returning any non-zero random value is sufficient for functional emulation.
