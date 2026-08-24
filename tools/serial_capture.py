#!/usr/bin/env python3
"""Capture UART output from ESP32-C3 via pyserial.

Usage: python3 serial_capture.py <port> <duration_seconds> [--reset]

Outputs captured text to stdout.
"""

import sys
import time

import serial


def main():
    port = sys.argv[1] if len(sys.argv) > 1 else "/dev/ttyESP32C3"
    duration = int(sys.argv[2]) if len(sys.argv) > 2 else 10
    do_reset = "--reset" in sys.argv

    s = serial.Serial(port, 115200, timeout=1)
    time.sleep(0.2)

    if do_reset:
        # Reset chip via DTR/RTS toggle
        s.dtr = False
        s.rts = True
        time.sleep(0.1)
        s.dtr = False
        s.rts = False
        time.sleep(0.1)

    end = time.time() + duration
    while time.time() < end:
        data = s.read(1024)
        if data:
            text = data.decode("utf-8", errors="replace")
            print(text, end="", flush=True)

    s.close()


if __name__ == "__main__":
    main()
