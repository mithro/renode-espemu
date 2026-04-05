// ESP32-C3 GDMA (General DMA) peripheral for Renode
//
// 3-channel DMA controller used by SPI, I2S, UHCI, AES, SHA, and ADC.
// Each channel has IN (RX) and OUT (TX) sub-channels with linked-list
// descriptor-based transfers.
//
// Base: 0x6003F000, Size: 0x1000
//
// This implementation provides register storage only — no actual DMA
// transfers are performed. Firmware can configure channels and read
// back settings; descriptor-based transfers are not emulated.

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class ESP32C3_GDMA : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_GDMA(Machine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // Per-channel interrupt registers (3 channels, stride 0x10)
            for (int ch = 0; ch < NumChannels; ch++)
            {
                long baseOff = ch * 0x10;
                ((Registers)(baseOff + 0x000)).Define(this)
                    .WithValueField(0, 13, name: $"INT_RAW_CH{ch}");
                ((Registers)(baseOff + 0x004)).Define(this)
                    .WithValueField(0, 13, mode: FieldMode.Read, name: $"INT_ST_CH{ch}");
                ((Registers)(baseOff + 0x008)).Define(this)
                    .WithValueField(0, 13, name: $"INT_ENA_CH{ch}");
                ((Registers)(baseOff + 0x00C)).Define(this)
                    .WithValueField(0, 13, mode: FieldMode.Write, name: $"INT_CLR_CH{ch}");
            }

            // Global control registers
            Registers.MiscConf.Define(this)
                .WithFlag(0, name: "AHBM_RST_INTER")
                .WithReservedBits(1, 1)
                .WithFlag(2, name: "ARB_PRI_DIS")
                .WithFlag(3, name: "CLK_EN");

            Registers.Date.Define(this, 0x02008E10)
                .WithValueField(0, 32, name: "DATE");

            // Per-channel IN (RX) and OUT (TX) registers
            for (int ch = 0; ch < NumChannels; ch++)
            {
                DefineChannelInRegisters(ch);
                DefineChannelOutRegisters(ch);
            }
        }

        private void DefineChannelInRegisters(int ch)
        {
            long baseOff = 0x070 + ch * ChannelStride;

            ((Registers)(baseOff + 0x00)).Define(this)
                .WithFlag(0, name: $"IN_RST_CH{ch}")
                .WithFlag(1, name: $"IN_LOOP_TEST_CH{ch}")
                .WithFlag(2, name: $"INDSCR_BURST_EN_CH{ch}")
                .WithFlag(3, name: $"INDATA_BURST_EN_CH{ch}")
                .WithFlag(4, name: $"MEM_TRANS_EN_CH{ch}");

            ((Registers)(baseOff + 0x04)).Define(this)
                .WithReservedBits(0, 12)
                .WithFlag(12, name: $"IN_CHECK_OWNER_CH{ch}");

            ((Registers)(baseOff + 0x08)).Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: $"INFIFO_FULL_CH{ch}")
                .WithFlag(1, mode: FieldMode.Read, name: $"INFIFO_EMPTY_CH{ch}",
                    valueProviderCallback: _ => true) // FIFO empty at idle
                .WithValueField(2, 6, mode: FieldMode.Read, name: $"INFIFO_CNT_CH{ch}");

            ((Registers)(baseOff + 0x0C)).Define(this, 0x00000800)
                .WithValueField(0, 12, mode: FieldMode.Read, name: $"INFIFO_RDATA_CH{ch}")
                .WithFlag(12, mode: FieldMode.Write, name: $"INFIFO_POP_CH{ch}");

            ((Registers)(baseOff + 0x10)).Define(this)
                .WithValueField(0, 20, name: $"INLINK_ADDR_CH{ch}")
                .WithFlag(20, name: $"INLINK_AUTO_RET_CH{ch}")
                .WithReservedBits(21, 7)
                .WithFlag(28, mode: FieldMode.Write, name: $"INLINK_STOP_CH{ch}")
                .WithFlag(29, mode: FieldMode.Write, name: $"INLINK_START_CH{ch}")
                .WithFlag(30, mode: FieldMode.Write, name: $"INLINK_RESTART_CH{ch}")
                .WithFlag(31, mode: FieldMode.Read, name: $"INLINK_PARK_CH{ch}",
                    valueProviderCallback: _ => true); // parked (idle)

            ((Registers)(baseOff + 0x14)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"IN_STATE_CH{ch}");

            ((Registers)(baseOff + 0x18)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"IN_SUC_EOF_DES_ADDR_CH{ch}");
            ((Registers)(baseOff + 0x1C)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"IN_ERR_EOF_DES_ADDR_CH{ch}");
            ((Registers)(baseOff + 0x20)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"IN_DSCR_CH{ch}");
            ((Registers)(baseOff + 0x24)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"IN_DSCR_BF0_CH{ch}");
            ((Registers)(baseOff + 0x28)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"IN_DSCR_BF1_CH{ch}");

            ((Registers)(baseOff + 0x2C)).Define(this)
                .WithValueField(0, 4, name: $"RX_PRI_CH{ch}");

            ((Registers)(baseOff + 0x30)).Define(this, 0x0000003F)
                .WithValueField(0, 6, name: $"PERI_IN_SEL_CH{ch}");
        }

        private void DefineChannelOutRegisters(int ch)
        {
            long baseOff = 0x0D0 + ch * ChannelStride;

            ((Registers)(baseOff + 0x00)).Define(this)
                .WithFlag(0, name: $"OUT_RST_CH{ch}")
                .WithFlag(1, name: $"OUT_LOOP_TEST_CH{ch}")
                .WithFlag(2, name: $"OUTDSCR_BURST_EN_CH{ch}")
                .WithFlag(3, name: $"OUTDATA_BURST_EN_CH{ch}")
                .WithFlag(4, name: $"OUT_EOF_MODE_CH{ch}");

            ((Registers)(baseOff + 0x04)).Define(this)
                .WithReservedBits(0, 12)
                .WithFlag(12, name: $"OUT_CHECK_OWNER_CH{ch}");

            ((Registers)(baseOff + 0x08)).Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: $"OUTFIFO_FULL_CH{ch}")
                .WithFlag(1, mode: FieldMode.Read, name: $"OUTFIFO_EMPTY_CH{ch}",
                    valueProviderCallback: _ => true)
                .WithValueField(2, 6, mode: FieldMode.Read, name: $"OUTFIFO_CNT_CH{ch}");

            ((Registers)(baseOff + 0x0C)).Define(this)
                .WithValueField(0, 9, name: $"OUTFIFO_WDATA_CH{ch}")
                .WithFlag(9, mode: FieldMode.Write, name: $"OUTFIFO_PUSH_CH{ch}");

            ((Registers)(baseOff + 0x10)).Define(this)
                .WithValueField(0, 20, name: $"OUTLINK_ADDR_CH{ch}")
                .WithReservedBits(20, 8)
                .WithFlag(28, mode: FieldMode.Write, name: $"OUTLINK_STOP_CH{ch}")
                .WithFlag(29, mode: FieldMode.Write, name: $"OUTLINK_START_CH{ch}")
                .WithFlag(30, mode: FieldMode.Write, name: $"OUTLINK_RESTART_CH{ch}")
                .WithFlag(31, mode: FieldMode.Read, name: $"OUTLINK_PARK_CH{ch}",
                    valueProviderCallback: _ => true);

            ((Registers)(baseOff + 0x14)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"OUT_STATE_CH{ch}");

            ((Registers)(baseOff + 0x18)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"OUT_EOF_DES_ADDR_CH{ch}");
            ((Registers)(baseOff + 0x1C)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"OUT_EOF_BFR_DES_ADDR_CH{ch}");
            ((Registers)(baseOff + 0x20)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"OUT_DSCR_CH{ch}");
            ((Registers)(baseOff + 0x24)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"OUT_DSCR_BF0_CH{ch}");
            ((Registers)(baseOff + 0x28)).Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: $"OUT_DSCR_BF1_CH{ch}");

            ((Registers)(baseOff + 0x2C)).Define(this)
                .WithValueField(0, 4, name: $"TX_PRI_CH{ch}");

            ((Registers)(baseOff + 0x30)).Define(this, 0x0000003F)
                .WithValueField(0, 6, name: $"PERI_OUT_SEL_CH{ch}");
        }

        private const int NumChannels = 3;
        private const long ChannelStride = 0xC0;

        private enum Registers : long
        {
            MiscConf = 0x044,
            Date     = 0x048,
        }
    }
}
