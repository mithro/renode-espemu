// ESP32-C3 SYSCON + RNG peripheral for Renode
//
// The SYSCON block at 0x60026000 contains miscellaneous system configuration.
// The hardware RNG register (WDEV_RND_REG) is at offset 0xB0 within this range.
// Reading it returns a random 32-bit value.
//
// This C# peripheral replaces the Python stub_syscon_rng.py.

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_RNG : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_RNG(Machine machine) : base(machine)
        {
            random = new PseudoRandom();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // WDEV_RND_REG at offset 0xB0: hardware random number generator
            Registers.WdevRnd.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "WDEV_RND",
                    valueProviderCallback: _ => (uint)random.Next());
        }

        private readonly PseudoRandom random;

        /// <summary>
        /// Simple xorshift32 PRNG for deterministic-but-varied random values.
        /// </summary>
        private class PseudoRandom
        {
            private uint state = 0xDEADBEEF;

            public int Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (int)state;
            }
        }

        private enum Registers : long
        {
            WdevRnd = 0xB0,
        }
    }
}
