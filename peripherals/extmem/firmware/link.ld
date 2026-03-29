/* Minimal linker script for ESP32-C3 bare-metal test firmware.
 * Places code in IRAM (internal SRAM, executable on reset).
 */

MEMORY
{
    IRAM (rwx) : ORIGIN = 0x40380000, LENGTH = 0x10000
}

ENTRY(main)

SECTIONS
{
    .text : {
        *(.text .text.*)
    } > IRAM

    .rodata : {
        *(.rodata .rodata.*)
    } > IRAM

    .data : {
        *(.data .data.*)
    } > IRAM

    .bss : {
        *(.bss .bss.*)
    } > IRAM
}
