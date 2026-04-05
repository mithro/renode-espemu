// ESP32-C3 MMU Table peripheral for Renode
//
// Flat array of 128 page table entries used by ICache/DCache to translate
// virtual addresses to physical flash pages.
//
// Base: 0x600C5000, Size: 0x1000 (only 0x200 used)
//
// Each entry: bit 8 = INVALID flag, bits 7:0 = physical page number.
// At reset, all entries are 0x100 (invalid). ROM writes entries to map
// flash content into the virtual address space.

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class ESP32C3_MMU : IDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_MMU()
        {
            entries = new uint[NumEntries];
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < NumEntries; i++)
            {
                entries[i] = InvalidEntry;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            int index = (int)(offset / 4);
            if (index < NumEntries)
                return entries[index];
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            int index = (int)(offset / 4);
            if (index < NumEntries)
                entries[index] = value;
        }

        public long Size => 0x1000;

        private readonly uint[] entries;
        private const int NumEntries = 128;
        private const uint InvalidEntry = 0x100; // bit 8 = invalid
    }
}
