// ESP32-C3 SYSCON peripheral for Renode
//
// The SYSCON block at 0x60026000 contains system configuration registers
// including WiFi/BT clock control, memory power, and the RNG data register.
//
// Register map from ESP-IDF syscon_reg.h (39 registers):
//   0x000: SYSCON_SYSCLK_CONF_REG
//   0x004: SYSCON_TICK_CONF_REG
//   0x008: SYSCON_CLK_OUT_EN_REG
//   0x00C: SYSCON_WIFI_BB_CFG_REG
//   0x010: SYSCON_WIFI_BB_CFG_2_REG
//   0x014: SYSCON_WIFI_CLK_EN_REG (critical: reset default 0xFFFFFFFF)
//   0x018: SYSCON_WIFI_RST_EN_REG
//   0x01C: SYSCON_HOST_INF_SEL_REG
//   0x020: SYSCON_EXT_MEM_PMS_LOCK_REG
//   0x028: SYSCON_FLASH_ACE0_ATTR_REG
//   0x02C: SYSCON_FLASH_ACE1_ATTR_REG
//   0x030: SYSCON_FLASH_ACE2_ATTR_REG
//   0x034: SYSCON_FLASH_ACE3_ATTR_REG
//   0x038: SYSCON_FLASH_ACE0_ADDR_REG
//   0x03C: SYSCON_FLASH_ACE1_ADDR_REG
//   0x040: SYSCON_FLASH_ACE2_ADDR_REG
//   0x044: SYSCON_FLASH_ACE3_ADDR_REG
//   0x048: SYSCON_FLASH_ACE0_SIZE_REG
//   0x04C: SYSCON_FLASH_ACE1_SIZE_REG
//   0x050: SYSCON_FLASH_ACE2_SIZE_REG
//   0x054: SYSCON_FLASH_ACE3_SIZE_REG
//   0x088: SYSCON_SPI_MEM_PMS_CTRL_REG
//   0x08C: SYSCON_SPI_MEM_REJECT_ADDR_REG
//   0x090: SYSCON_SDIO_CTRL_REG
//   0x094: SYSCON_REDCY_SIG0_REG
//   0x098: SYSCON_REDCY_SIG1_REG
//   0x09C: SYSCON_FRONT_END_MEM_PD_REG
//   0x0A0: SYSCON_RETENTION_CTRL_REG
//   0x0A4: SYSCON_CLKGATE_FORCE_ON_REG
//   0x0A8: SYSCON_MEM_POWER_DOWN_REG
//   0x0AC: SYSCON_MEM_POWER_UP_REG
//   0x0B0: SYSCON_RND_DATA_REG (hardware RNG, read-only)
//   0x0B4: SYSCON_PERI_BACKUP_CONFIG_REG
//   0x0B8: SYSCON_PERI_BACKUP_APB_ADDR_REG
//   0x0BC: SYSCON_PERI_BACKUP_MEM_ADDR_REG
//   0x0C0: SYSCON_PERI_BACKUP_INT_RAW_REG
//   0x0C4: SYSCON_PERI_BACKUP_INT_ST_REG
//   0x0C8: SYSCON_PERI_BACKUP_INT_ENA_REG
//   0x0D0: SYSCON_PERI_BACKUP_INT_CLR_REG
//   0x3FC: SYSCON_DATE_REG

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
            random.Reset();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // 0x000: SYSCON_SYSCLK_CONF_REG
            Registers.SysclkConf.Define(this, 0x00000001)
                .WithValueField(0, 10, name: "PRE_DIV_CNT")
                .WithFlag(10, name: "CLK_320M_EN")
                .WithFlag(11, name: "CLK_EN")
                .WithFlag(12, name: "RST_TICK_CNT")
                .WithReservedBits(13, 19);

            // 0x004: SYSCON_TICK_CONF_REG
            Registers.TickConf.Define(this, 0x00010727)
                .WithValueField(0, 8, name: "XTAL_TICK_NUM")
                .WithValueField(8, 8, name: "CK8M_TICK_NUM")
                .WithFlag(16, name: "TICK_ENABLE")
                .WithReservedBits(17, 15);

            // 0x008: SYSCON_CLK_OUT_EN_REG
            Registers.ClkOutEn.Define(this, 0x000007FF)
                .WithFlag(0, name: "CLK20_OEN")
                .WithFlag(1, name: "CLK22_OEN")
                .WithFlag(2, name: "CLK44_OEN")
                .WithFlag(3, name: "CLK_BB_OEN")
                .WithFlag(4, name: "CLK80_OEN")
                .WithFlag(5, name: "CLK160_OEN")
                .WithFlag(6, name: "CLK_320M_OEN")
                .WithFlag(7, name: "CLK_ADC_INF_OEN")
                .WithFlag(8, name: "CLK_DAC_CPU_OEN")
                .WithFlag(9, name: "CLK40X_BB_OEN")
                .WithFlag(10, name: "CLK_XTAL_OEN")
                .WithReservedBits(11, 21);

            // 0x00C: SYSCON_WIFI_BB_CFG_REG
            Registers.WifiBbCfg.Define(this)
                .WithValueField(0, 32, name: "WIFI_BB_CFG");

            // 0x010: SYSCON_WIFI_BB_CFG_2_REG
            Registers.WifiBbCfg2.Define(this)
                .WithValueField(0, 32, name: "WIFI_BB_CFG_2");

            // 0x014: SYSCON_WIFI_CLK_EN_REG (default: all clocks enabled)
            Registers.WifiClkEn.Define(this, 0xFFFFFFFF)
                .WithValueField(0, 32, name: "WIFI_CLK_EN");

            // 0x018: SYSCON_WIFI_RST_EN_REG
            Registers.WifiRstEn.Define(this)
                .WithValueField(0, 32, name: "WIFI_RST_EN");

            // 0x01C: SYSCON_HOST_INF_SEL_REG
            Registers.HostInfSel.Define(this)
                .WithValueField(0, 32, name: "HOST_INF_SEL");

            // 0x020: SYSCON_EXT_MEM_PMS_LOCK_REG
            Registers.ExtMemPmsLock.Define(this)
                .WithFlag(0, name: "EXT_MEM_PMS_LOCK")
                .WithReservedBits(1, 31);

            // 0x028: SYSCON_FLASH_ACE0_ATTR_REG
            Registers.FlashAce0Attr.Define(this, 0x00000003)
                .WithValueField(0, 2, name: "FLASH_ACE0_ATTR")
                .WithReservedBits(2, 30);

            // 0x02C: SYSCON_FLASH_ACE1_ATTR_REG
            Registers.FlashAce1Attr.Define(this, 0x00000003)
                .WithValueField(0, 2, name: "FLASH_ACE1_ATTR")
                .WithReservedBits(2, 30);

            // 0x030: SYSCON_FLASH_ACE2_ATTR_REG
            Registers.FlashAce2Attr.Define(this, 0x00000003)
                .WithValueField(0, 2, name: "FLASH_ACE2_ATTR")
                .WithReservedBits(2, 30);

            // 0x034: SYSCON_FLASH_ACE3_ATTR_REG
            Registers.FlashAce3Attr.Define(this, 0x00000003)
                .WithValueField(0, 2, name: "FLASH_ACE3_ATTR")
                .WithReservedBits(2, 30);

            // 0x038: SYSCON_FLASH_ACE0_ADDR_REG
            Registers.FlashAce0Addr.Define(this)
                .WithValueField(0, 32, name: "FLASH_ACE0_ADDR_S");

            // 0x03C: SYSCON_FLASH_ACE1_ADDR_REG
            Registers.FlashAce1Addr.Define(this, 0x00400000)
                .WithValueField(0, 32, name: "FLASH_ACE1_ADDR_S");

            // 0x040: SYSCON_FLASH_ACE2_ADDR_REG
            Registers.FlashAce2Addr.Define(this, 0x00800000)
                .WithValueField(0, 32, name: "FLASH_ACE2_ADDR_S");

            // 0x044: SYSCON_FLASH_ACE3_ADDR_REG
            Registers.FlashAce3Addr.Define(this, 0x00C00000)
                .WithValueField(0, 32, name: "FLASH_ACE3_ADDR_S");

            // 0x048: SYSCON_FLASH_ACE0_SIZE_REG
            Registers.FlashAce0Size.Define(this, 0x00000400)
                .WithValueField(0, 13, name: "FLASH_ACE0_SIZE")
                .WithReservedBits(13, 19);

            // 0x04C: SYSCON_FLASH_ACE1_SIZE_REG
            Registers.FlashAce1Size.Define(this, 0x00000400)
                .WithValueField(0, 13, name: "FLASH_ACE1_SIZE")
                .WithReservedBits(13, 19);

            // 0x050: SYSCON_FLASH_ACE2_SIZE_REG
            Registers.FlashAce2Size.Define(this, 0x00000400)
                .WithValueField(0, 13, name: "FLASH_ACE2_SIZE")
                .WithReservedBits(13, 19);

            // 0x054: SYSCON_FLASH_ACE3_SIZE_REG
            Registers.FlashAce3Size.Define(this, 0x00000400)
                .WithValueField(0, 13, name: "FLASH_ACE3_SIZE")
                .WithReservedBits(13, 19);

            // 0x088: SYSCON_SPI_MEM_PMS_CTRL_REG
            Registers.SpiMemPmsCtrl.Define(this)
                .WithFlag(0, name: "SPI_MEM_REJECT_INT")
                .WithFlag(1, name: "SPI_MEM_REJECT_CLR")
                .WithFlag(2, name: "SPI_MEM_REJECT_CDE")
                .WithReservedBits(3, 29);

            // 0x08C: SYSCON_SPI_MEM_REJECT_ADDR_REG
            Registers.SpiMemRejectAddr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "SPI_MEM_REJECT_ADDR");

            // 0x090: SYSCON_SDIO_CTRL_REG
            Registers.SdioCtrl.Define(this)
                .WithFlag(0, name: "SDIO_WIN_ACCESS_EN")
                .WithReservedBits(1, 31);

            // 0x094: SYSCON_REDCY_SIG0_REG
            Registers.RedcySig0.Define(this)
                .WithValueField(0, 31, name: "REDCY_SIG0")
                .WithFlag(31, mode: FieldMode.Read, name: "REDCY_ANDOR");

            // 0x098: SYSCON_REDCY_SIG1_REG
            Registers.RedcySig1.Define(this)
                .WithValueField(0, 31, name: "REDCY_SIG1")
                .WithFlag(31, mode: FieldMode.Read, name: "REDCY_NANDNOR");

            // 0x09C: SYSCON_FRONT_END_MEM_PD_REG
            Registers.FrontEndMemPd.Define(this)
                .WithFlag(0, name: "AGC_MEM_FORCE_PU")
                .WithFlag(1, name: "AGC_MEM_FORCE_PD")
                .WithFlag(2, name: "PBUS_MEM_FORCE_PU")
                .WithFlag(3, name: "PBUS_MEM_FORCE_PD")
                .WithFlag(4, name: "DC_MEM_FORCE_PU")
                .WithFlag(5, name: "DC_MEM_FORCE_PD")
                .WithReservedBits(6, 26);

            // 0x0A0: SYSCON_RETENTION_CTRL_REG
            Registers.RetentionCtrl.Define(this)
                .WithValueField(0, 32, name: "RETENTION_CTRL");

            // 0x0A4: SYSCON_CLKGATE_FORCE_ON_REG
            Registers.ClkgateForceOn.Define(this, 0x3F)
                .WithFlag(0, name: "ROM_CLKGATE_FORCE_ON")
                .WithFlag(1, name: "SRAM_CLKGATE_FORCE_ON")
                .WithReservedBits(2, 30);

            // 0x0A8: SYSCON_MEM_POWER_DOWN_REG
            Registers.MemPowerDown.Define(this)
                .WithValueField(0, 3, name: "ROM_POWER_DOWN")
                .WithValueField(3, 3, name: "SRAM_POWER_DOWN")
                .WithReservedBits(6, 26);

            // 0x0AC: SYSCON_MEM_POWER_UP_REG
            Registers.MemPowerUp.Define(this, 0x3F)
                .WithValueField(0, 3, name: "ROM_POWER_UP")
                .WithValueField(3, 3, name: "SRAM_POWER_UP")
                .WithReservedBits(6, 26);

            // 0x0B0: SYSCON_RND_DATA_REG (hardware random number generator)
            Registers.RndData.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "RND_DATA",
                    valueProviderCallback: _ => random.Next());

            // 0x0B4: SYSCON_PERI_BACKUP_CONFIG_REG
            Registers.PeriBackupConfig.Define(this)
                .WithValueField(0, 32, name: "PERI_BACKUP_CONFIG");

            // 0x0B8: SYSCON_PERI_BACKUP_APB_ADDR_REG
            Registers.PeriBackupApbAddr.Define(this)
                .WithValueField(0, 32, name: "BACKUP_APB_START_ADDR");

            // 0x0BC: SYSCON_PERI_BACKUP_MEM_ADDR_REG
            Registers.PeriBackupMemAddr.Define(this)
                .WithValueField(0, 32, name: "BACKUP_MEM_START_ADDR");

            // 0x0C0: SYSCON_PERI_BACKUP_INT_RAW_REG
            Registers.PeriBackupIntRaw.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "PERI_BACKUP_DONE_INT_RAW")
                .WithFlag(1, mode: FieldMode.Read, name: "PERI_BACKUP_ERR_INT_RAW")
                .WithReservedBits(2, 30);

            // 0x0C4: SYSCON_PERI_BACKUP_INT_ST_REG
            Registers.PeriBackupIntSt.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "PERI_BACKUP_DONE_INT_ST")
                .WithFlag(1, mode: FieldMode.Read, name: "PERI_BACKUP_ERR_INT_ST")
                .WithReservedBits(2, 30);

            // 0x0C8: SYSCON_PERI_BACKUP_INT_ENA_REG
            Registers.PeriBackupIntEna.Define(this)
                .WithFlag(0, name: "PERI_BACKUP_DONE_INT_ENA")
                .WithFlag(1, name: "PERI_BACKUP_ERR_INT_ENA")
                .WithReservedBits(2, 30);

            // 0x0D0: SYSCON_PERI_BACKUP_INT_CLR_REG
            Registers.PeriBackupIntClr.Define(this)
                .WithFlag(0, mode: FieldMode.Write, name: "PERI_BACKUP_DONE_INT_CLR")
                .WithFlag(1, mode: FieldMode.Write, name: "PERI_BACKUP_ERR_INT_CLR")
                .WithReservedBits(2, 30);

            // 0x3FC: SYSCON_DATE_REG (hardware reads 0x02007210)
            Registers.Date.Define(this, 0x02007210)
                .WithValueField(0, 28, name: "DATE")
                .WithReservedBits(28, 4);
        }

        private readonly PseudoRandom random;

        private class PseudoRandom
        {
            private uint state = InitialState;

            public uint Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }

            public void Reset()
            {
                state = InitialState;
            }

            private const uint InitialState = 0xDEADBEEF;
        }

        private enum Registers : long
        {
            SysclkConf = 0x000,
            TickConf = 0x004,
            ClkOutEn = 0x008,
            WifiBbCfg = 0x00C,
            WifiBbCfg2 = 0x010,
            WifiClkEn = 0x014,
            WifiRstEn = 0x018,
            HostInfSel = 0x01C,
            ExtMemPmsLock = 0x020,
            FlashAce0Attr = 0x028,
            FlashAce1Attr = 0x02C,
            FlashAce2Attr = 0x030,
            FlashAce3Attr = 0x034,
            FlashAce0Addr = 0x038,
            FlashAce1Addr = 0x03C,
            FlashAce2Addr = 0x040,
            FlashAce3Addr = 0x044,
            FlashAce0Size = 0x048,
            FlashAce1Size = 0x04C,
            FlashAce2Size = 0x050,
            FlashAce3Size = 0x054,
            SpiMemPmsCtrl = 0x088,
            SpiMemRejectAddr = 0x08C,
            SdioCtrl = 0x090,
            RedcySig0 = 0x094,
            RedcySig1 = 0x098,
            FrontEndMemPd = 0x09C,
            RetentionCtrl = 0x0A0,
            ClkgateForceOn = 0x0A4,
            MemPowerDown = 0x0A8,
            MemPowerUp = 0x0AC,
            RndData = 0x0B0,
            PeriBackupConfig = 0x0B4,
            PeriBackupApbAddr = 0x0B8,
            PeriBackupMemAddr = 0x0BC,
            PeriBackupIntRaw = 0x0C0,
            PeriBackupIntSt = 0x0C4,
            PeriBackupIntEna = 0x0C8,
            PeriBackupIntClr = 0x0D0,
            Date = 0x3FC,
        }
    }
}
