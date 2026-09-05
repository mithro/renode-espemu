# Vendored product decoder sources

These four files are the ACTUAL shipped Fine Offset / Ecowitt FSK decoders,
copied verbatim from the firmware product repo:

    esp32-to-433mhz : firmware/decoders/decode_fineoffset.c
                      firmware/decoders/decode_fineoffset.h
                      firmware/decoders/decode_common.c
                      firmware/decoders/decode_common.h

Provenance at copy time:
    source commit touching the decoders: 4c85365 (2026-09-05)
    esp32-to-433mhz HEAD:                cd95b48 (branch add-tasmota-firmware)

Why they live here (copied, not symlinked)
------------------------------------------
The Renode CI job (esp32-to-433mhz/.github/workflows/firmware.yml -> `renode`)
checks out ONLY this repo (mithro/renode-espemu); the firmware repo is not on
disk in that job, so a symlink or a relative CMake path into the firmware tree
would not resolve. The files are therefore copied in.

The cc1101_decode test firmware COMPILES these copies directly (see
../CMakeLists.txt SRCS), so the emulation exercises the real product decoder,
not a reimplemented proxy.

SYNC RISK
---------
Because these are copies, they can drift from the firmware repo. If
firmware/decoders/decode_fineoffset.c or decode_common.c changes there, refresh
the copies here and re-run peripherals/cc1101_decode/test.robot. The host-side
decoder tests (esp32-to-433mhz firmware/tests/) remain the primary guard on the
decoder itself; this copy guards that the SAME logic still decodes correctly
when built for and run on the emulated ESP32-C3 target off the CC1101 RX FIFO.
