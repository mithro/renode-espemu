"""ESP32-C3 SYSCON stub with RNG (WDEV_RND_REG at offset 0xB0).

The hardware RNG register at 0x600260B0 returns random 32-bit values.
We use a simple LFSR to generate pseudo-random data in emulation.
All other SYSCON registers return 0.
"""

if request.IsInit:
    rng_state = 0xDEADBEEF
    syscon_regs = {}

if request.IsRead:
    if request.Offset == 0xB0:
        # WDEV_RND_REG: return pseudo-random value via LFSR
        rng_state ^= (rng_state << 13) & 0xFFFFFFFF
        rng_state ^= (rng_state >> 17) & 0xFFFFFFFF
        rng_state ^= (rng_state << 5) & 0xFFFFFFFF
        rng_state &= 0xFFFFFFFF
        request.Value = rng_state
    else:
        request.Value = syscon_regs.get(request.Offset, 0)
else:
    syscon_regs[request.Offset] = request.Value
