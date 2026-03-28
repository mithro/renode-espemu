# ESP32 Hardware Crypto Accelerators

## Overview

The ESP32 includes three hardware cryptographic accelerators: AES, SHA, and RSA (MPI - Multi-Precision Integer). These accelerators offload computationally intensive cryptographic operations from the CPU, providing significant speedup for TLS connections, secure boot verification, flash encryption/decryption, and general-purpose cryptography.

All three accelerators are memory-mapped peripherals that operate by having the CPU write input data to registers, trigger the operation, and read back results. They are independent blocks that can operate concurrently (though each individual block processes one operation at a time).

Espressif's QEMU fork implements all three crypto blocks, providing a solid reference for Renode implementation.

## Hardware Specifications

### AES Accelerator
- **Algorithms**: AES-128, AES-192, AES-256
- **Modes**: ECB (block-level; software implements CBC/CTR/etc. by chaining ECB operations)
- **Key sizes**: 128-bit (16 bytes), 192-bit (24 bytes), 256-bit (32 bytes)
- **Block size**: 128 bits (16 bytes) per operation
- **Operation**: Write key and plaintext/ciphertext to registers, set mode (encrypt/decrypt) and key length, trigger start, poll for completion, read result
- **Endianness**: Byte order in registers follows AES standard (big-endian block format)
- **Performance**: Single-cycle per AES round in hardware; full block encryption in ~12-15 clock cycles
- **Base address**: 0x3FF01000

### SHA Accelerator
- **Algorithms**: SHA-1, SHA-224, SHA-256, SHA-384, SHA-512
- **Block sizes**:
  - SHA-1/224/256: 64-byte (512-bit) input blocks
  - SHA-384/512: 128-byte (1024-bit) input blocks
- **Hash output sizes**: SHA-1 (160-bit), SHA-224 (224-bit), SHA-256 (256-bit), SHA-384 (384-bit), SHA-512 (512-bit)
- **Operation**: Write message block to registers, trigger hash computation (start or continue), poll for completion, read hash state/result
- **Supports incremental hashing**: Can process multiple blocks sequentially, maintaining intermediate hash state in registers
- **Base address**: 0x3FF03000

### RSA/MPI Accelerator
- **Operand sizes**: Up to 4096 bits (configurable in 512-bit increments)
- **Operations**:
  - Modular exponentiation: `Z = X^Y mod M` (core RSA operation)
  - Modular multiplication: `Z = X * Y mod M`
  - Large number multiplication: `Z = X * Y`
- **Montgomery multiplication**: Hardware-accelerated Montgomery multiplication with configurable operand length
- **Memory**: Uses dedicated SRAM blocks for operands (X, Y, Z, M memory banks)
- **Performance**: Dramatically faster than software -- 4096-bit modular exponentiation completes in milliseconds vs. seconds in software
- **Base address**: 0x3FF02000

## TRM Chapter Reference

- **Chapter 20**: AES Accelerator
  - Section 20.1: Features and operation modes
  - Section 20.2: Register description
  - Section 20.3: Operation flow
- **Chapter 21**: SHA Accelerator
  - Section 21.1: Features and supported algorithms
  - Section 21.2: Register description
  - Section 21.3: Operation flow (initial block, subsequent blocks)
- **Chapter 22**: RSA Accelerator
  - Section 22.1: Features and operation modes
  - Section 22.2: Register description
  - Section 22.3: Montgomery multiplication

TRM PDF: https://www.espressif.com/sites/default/files/documentation/esp32_technical_reference_manual_en.pdf

## Register Map Summary

### AES Registers (base 0x3FF01000)

| Register              | Offset      | Description                                     |
|-----------------------|-------------|-------------------------------------------------|
| AES_KEY_0_REG - AES_KEY_7_REG | 0x000-0x01C | AES key (8 x 32-bit words = 256 bits max) |
| AES_TEXT_0_REG - AES_TEXT_3_REG | 0x020-0x02C | Input/output data block (4 x 32-bit = 128 bits) |
| AES_MODE_REG          | 0x030       | Mode select: [0]=encrypt/decrypt, [2:1]=key size (0=128,1=192,2=256) |
| AES_START_REG         | 0x034       | Write 1 to start AES operation                  |
| AES_IDLE_REG          | 0x038       | Reads 1 when AES engine is idle (operation complete) |
| AES_ENDIAN_REG        | 0x040       | Endianness configuration                        |

### SHA Registers (base 0x3FF03000)

| Register              | Offset      | Description                                     |
|-----------------------|-------------|-------------------------------------------------|
| SHA_TEXT_0_REG - SHA_TEXT_31_REG | 0x080-0x0FC | Message block input (32 x 32-bit = 1024 bits max) |
| SHA_1_START_REG       | 0x080 (*)   | Start SHA-1 hash (first block)                  |
| SHA_1_CONTINUE_REG    | 0x084       | Continue SHA-1 hash (subsequent blocks)          |
| SHA_1_LOAD_REG        | 0x088       | Load SHA-1 hash result back into registers       |
| SHA_1_BUSY_REG        | 0x08C       | SHA-1 busy status                                |
| SHA_256_START_REG     | 0x090       | Start SHA-256 hash (first block)                 |
| SHA_256_CONTINUE_REG  | 0x094       | Continue SHA-256 hash (subsequent blocks)        |
| SHA_256_LOAD_REG      | 0x098       | Load SHA-256 result                              |
| SHA_256_BUSY_REG      | 0x09C       | SHA-256 busy status                              |
| SHA_384_START_REG     | 0x0A0       | Start SHA-384 hash (first block)                 |
| SHA_384_CONTINUE_REG  | 0x0A4       | Continue SHA-384 hash (subsequent blocks)        |
| SHA_384_LOAD_REG      | 0x0A8       | Load SHA-384 result                              |
| SHA_384_BUSY_REG      | 0x0AC       | SHA-384 busy status                              |
| SHA_512_START_REG     | 0x0B0       | Start SHA-512 hash (first block)                 |
| SHA_512_CONTINUE_REG  | 0x0B4       | Continue SHA-512 hash (subsequent blocks)        |
| SHA_512_LOAD_REG      | 0x0B8       | Load SHA-512 result                              |
| SHA_512_BUSY_REG      | 0x0BC       | SHA-512 busy status                              |

Note: SHA register offsets are algorithm-dependent. The hash state (intermediate/final hash values) is read from the SHA_H_x registers at the base of the SHA register space.

| Register              | Offset      | Description                                     |
|-----------------------|-------------|-------------------------------------------------|
| SHA_H_0_REG - SHA_H_15_REG | 0x000-0x03C | Hash state registers (16 x 32-bit = 512 bits, for SHA-384/512) |

### RSA/MPI Registers (base 0x3FF02000)

| Register              | Offset      | Description                                     |
|-----------------------|-------------|-------------------------------------------------|
| RSA_MEM_M_BLOCK_BASE  | 0x000       | Modulus M memory (up to 4096 bits = 128 words)   |
| RSA_MEM_Z_BLOCK_BASE  | 0x200       | Result Z memory (up to 4096 bits)                |
| RSA_MEM_Y_BLOCK_BASE  | 0x400       | Operand Y memory (up to 4096 bits)               |
| RSA_MEM_X_BLOCK_BASE  | 0x600       | Operand X / Exponent memory (up to 4096 bits)    |
| RSA_M_DASH_REG        | 0x800       | M' (Montgomery parameter: -M^(-1) mod 2^32)     |
| RSA_MODEXP_MODE_REG   | 0x804       | Modular exponentiation operand length (in 32-bit words / 16 - 1) |
| RSA_MODEXP_START_REG  | 0x808       | Start modular exponentiation                     |
| RSA_MODMULT_MODE_REG  | 0x80C       | Modular multiplication operand length            |
| RSA_MODMULT_START_REG | 0x810       | Start modular multiplication                     |
| RSA_MULT_MODE_REG     | 0x814       | Multiplication operand length                    |
| RSA_MULT_START_REG    | 0x818       | Start multiplication                             |
| RSA_CLEAN_REG         | 0x81C       | Reads 1 when operand memory is zeroed after reset |
| RSA_IDLE_REG          | 0x820       | Reads 1 when RSA engine is idle                  |
| RSA_INTERRUPT_REG     | 0x824       | Interrupt status and clear                       |

## Source Code References

### SOC Register Definitions
- **Hardware crypto register addresses**: https://github.com/espressif/esp-idf/blob/master/components/soc/esp32/include/soc/hwcrypto_reg.h
  - Base addresses and register offsets for AES, SHA, and RSA

### HAL Layer
- **AES low-level operations**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_security/esp32/include/hal/aes_ll.h
  - `aes_ll_set_key()` - Write key to AES key registers
  - `aes_ll_set_block()` - Write data block to AES text registers
  - `aes_ll_read_block()` - Read result from AES text registers
  - `aes_ll_set_mode()` - Set encrypt/decrypt and key size
  - `aes_ll_start_transform()` - Trigger AES operation
  - `aes_ll_is_idle()` - Check if operation is complete

- **SHA low-level operations**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_security/esp32/include/hal/sha_ll.h
  - `sha_ll_fill_text_block()` - Write message block to SHA text registers
  - `sha_ll_start_block()` - Start hashing first block
  - `sha_ll_continue_block()` - Continue hashing subsequent block
  - `sha_ll_read_digest()` - Read hash result
  - `sha_ll_busy()` - Check if SHA is processing

- **MPI/RSA low-level operations**: https://github.com/espressif/esp-idf/blob/master/components/esp_hal_security/esp32/include/hal/mpi_ll.h
  - `mpi_ll_set_mode()` - Set operand length
  - `mpi_ll_start_modexp()` - Start modular exponentiation
  - `mpi_ll_start_modmult()` - Start modular multiplication
  - `mpi_ll_start_mult()` - Start multiplication
  - `mpi_ll_is_idle()` - Check completion
  - `mpi_ll_write_m_prime()` - Write Montgomery parameter

### QEMU Implementation
- **AES model**: https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_aes.c
  - Implements full AES-128/192/256 encryption and decryption
  - Uses QEMU's built-in crypto libraries for actual AES computation
  - Handles key register writes, mode selection, start trigger, and idle polling

- **SHA model**: https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_sha.c
  - Implements SHA-1, SHA-256, SHA-384, SHA-512
  - Supports start (first block) and continue (subsequent blocks) operations
  - Manages hash state across multiple blocks

- **RSA model**: https://github.com/espressif/qemu/blob/esp-develop/hw/misc/esp32_rsa.c
  - Implements modular exponentiation, modular multiplication, and multiplication
  - Uses big-number arithmetic libraries
  - Handles variable operand lengths up to 4096 bits

## Renode Implementation Analysis

### Reference Peripherals in Renode
- Renode has some crypto peripheral implementations for other SoCs that can serve as reference
- The pattern is typically: register-based I/O with software-computed results written back instantly

### Implementation Approach

1. **AES Accelerator (`ESP32_AES`)**:
   - Implement 8 key registers (32-bit each) and 4 text registers (32-bit each)
   - On write to `AES_START_REG`:
     - Read key from key registers based on mode (128/192/256-bit)
     - Read input block from text registers
     - Perform AES encrypt or decrypt using C# `System.Security.Cryptography.Aes` or Bouncy Castle
     - Write result back to text registers
     - Set `AES_IDLE_REG` to 1
   - Operation completes immediately (no cycle-accurate timing needed)
   - Endianness handling per `AES_ENDIAN_REG`

2. **SHA Accelerator (`ESP32_SHA`)**:
   - Implement 32 text input registers and 16 hash state registers
   - Support two operation types per algorithm:
     - **Start**: Initialize hash state, process first message block
     - **Continue**: Use existing hash state, process next message block
   - Use C# `System.Security.Cryptography.SHA256` etc., but note: the ESP32 SHA operates at the block compression level, not the full-message level
   - **Key insight**: The hardware exposes the intermediate hash state after each block. Standard library SHA APIs typically don't expose intermediate state. Implementation may need a manual SHA compression function or a library that supports incremental hashing at the block level
   - Set busy register to 0 after computation completes

3. **RSA/MPI Accelerator (`ESP32_RSA`)**:
   - Implement four large memory blocks (M, Z, Y, X) each up to 128 x 32-bit words (4096 bits)
   - On write to start registers:
     - Read operands from memory blocks based on configured length
     - Perform the requested operation (modexp, modmult, or mult)
     - Write result to Z memory block
     - Set `RSA_IDLE_REG` to 1
   - Use C# `System.Numerics.BigInteger` for arbitrary-precision arithmetic
   - Montgomery parameter (M') is computed by software and written to a register; the hardware uses it internally
   - For modular exponentiation: `Z = X^Y mod M` using the standard square-and-multiply algorithm

4. **Clock Gating Integration**:
   - All three crypto blocks have clock gate bits in DPORT_PERIP_CLK_EN_REG (via DPORT_WIFI_CLK_EN_REG for crypto)
   - Firmware enables crypto clocks before use and disables after
   - In emulation, access should work regardless of clock gate state (for simplicity)

5. **Interrupt Support**:
   - RSA has an interrupt (`RSA_INTERRUPT_REG`) that fires on operation completion
   - AES and SHA are typically polled (no interrupts)
   - RSA interrupt should be routed through the interrupt matrix

## Complexity Assessment

**Overall Complexity: MEDIUM**

| Aspect                        | Difficulty | Notes                                                |
|-------------------------------|------------|------------------------------------------------------|
| AES implementation            | Low        | Standard AES with well-defined registers; C# has built-in support |
| SHA implementation            | Medium     | Block-level compression function exposure requires careful implementation |
| RSA implementation            | Medium-High| Large operand handling, big-number arithmetic, Montgomery multiplication |
| Register interfaces           | Low        | All three have simple register-mapped interfaces     |
| QEMU reference quality        | High       | All three have complete QEMU implementations to reference |
| Testing                       | Low-Medium | Can be tested with known test vectors                |
| Endianness handling           | Medium     | Must match hardware byte ordering precisely          |

**Estimated effort**:
- AES: 3-5 days
- SHA: 1-2 weeks (due to block-level hash state management)
- RSA: 1-2 weeks (due to big-number arithmetic)
- Total: 3-4 weeks for all three

**Priority**: MEDIUM-HIGH -- Crypto accelerators are used by:
- Secure boot verification (SHA + RSA)
- Flash encryption/decryption (AES) -- required if flash encryption is enabled
- TLS connections (all three)
- General application cryptography
- WiFi WPA2 (AES)

If the target firmware uses flash encryption, AES is required. If secure boot is enabled, SHA and RSA are required. For unencrypted, non-secure-boot firmware, crypto can be deferred.

**Dependencies**:
- DPORT (for clock gating)
- Interrupt matrix (for RSA interrupt)
- No other peripheral dependencies -- crypto blocks are self-contained

**Risk factors**:
- Endianness mismatches can produce completely wrong results with no obvious error
- SHA block-level state management differs from typical high-level SHA APIs
- RSA operand memory layout and byte ordering must be exact
- Montgomery multiplication parameter (M') validation
