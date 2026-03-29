CROSS   ?= riscv32-esp-elf-
CC       = $(CROSS)gcc
OBJCOPY  = $(CROSS)objcopy

CFLAGS  = -march=rv32imc -mabi=ilp32 -Os -nostdlib -ffreestanding
LDFLAGS = -T link.ld -nostartfiles

TARGET  = extmem_test

.PHONY: all clean

all: $(TARGET).bin

$(TARGET).elf: main.c link.ld
	$(CC) $(CFLAGS) $(LDFLAGS) -o $@ main.c

$(TARGET).bin: $(TARGET).elf
	$(OBJCOPY) -O binary $< $@

clean:
	rm -f $(TARGET).elf $(TARGET).bin
