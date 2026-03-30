// ESP32-C3 Sensitive (PMS - Permission Management System) Controller for Renode
//
// Manages memory protection and peripheral access permissions.
// The firmware configures PMS boundaries, permission constraints,
// and lock bits during boot to isolate memory regions.
//
// Base: DR_REG_SENSITIVE_BASE = 0x600C1000, Size: 0x1000
//
// Register groups:
//   - ROM/SRAM usage and lock registers
//   - DMA peripheral PMS constraints (SPI2, UHCI0, I2S0, MAC, etc.)
//   - IRAM0/DRAM0 split line constraints
//   - Core PIF PMS constraints (peripheral access per world)
//   - Region PMS constraints (address boundaries)
//   - PMS violation monitors (read-only status)
//   - Backup bus PMS constraints and monitor
//
// All 94 registers defined from sensitive_reg.h with correct reset values.

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_Sensitive : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_Sensitive(Machine machine) : base(machine)
        {
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // SENSITIVE_ROM_TABLE_LOCK_REG: 0x000
            Registers.RomTableLock.Define(this)
                .WithFlag(0, name: "SENSITIVE_ROM_TABLE_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_ROM_TABLE_REG: 0x004
            Registers.RomTable.Define(this)
                .WithValueField(0, 32, name: "SENSITIVE_ROM_TABLE");

            // SENSITIVE_PRIVILEGE_MODE_SEL_LOCK_REG: 0x008
            Registers.PrivilegeModeSelLock.Define(this)
                .WithFlag(0, name: "SENSITIVE_PRIVILEGE_MODE_SEL_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_PRIVILEGE_MODE_SEL_REG: 0x00C
            Registers.PrivilegeModeSel.Define(this)
                .WithFlag(0, name: "SENSITIVE_PRIVILEGE_MODE_SEL")
                .WithReservedBits(1, 31);

            // SENSITIVE_APB_PERIPHERAL_ACCESS_0_REG: 0x010
            Registers.ApbPeripheralAccess0.Define(this)
                .WithFlag(0, name: "SENSITIVE_APB_PERIPHERAL_ACCESS_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_APB_PERIPHERAL_ACCESS_1_REG: 0x014
            Registers.ApbPeripheralAccess1.Define(this, 0x00000001)
                .WithFlag(0, name: "SENSITIVE_APB_PERIPHERAL_ACCESS_SPLIT_BURST")
                .WithReservedBits(1, 31);

            // SENSITIVE_INTERNAL_SRAM_USAGE_0_REG: 0x018
            Registers.InternalSramUsage0.Define(this)
                .WithFlag(0, name: "SENSITIVE_INTERNAL_SRAM_USAGE_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_INTERNAL_SRAM_USAGE_1_REG: 0x01C
            Registers.InternalSramUsage1.Define(this, 0x0000000F)
                .WithFlag(0, name: "SENSITIVE_INTERNAL_SRAM_USAGE_CPU_CACHE")
                .WithValueField(1, 3, name: "SENSITIVE_INTERNAL_SRAM_USAGE_CPU_SRAM")
                .WithReservedBits(4, 28);

            // SENSITIVE_INTERNAL_SRAM_USAGE_3_REG: 0x020
            Registers.InternalSramUsage3.Define(this)
                .WithValueField(0, 3, name: "SENSITIVE_INTERNAL_SRAM_USAGE_MAC_DUMP_SRAM")
                .WithFlag(3, name: "SENSITIVE_INTERNAL_SRAM_ALLOC_MAC_DUMP")
                .WithReservedBits(4, 28);

            // SENSITIVE_INTERNAL_SRAM_USAGE_4_REG: 0x024
            Registers.InternalSramUsage4.Define(this)
                .WithFlag(0, name: "SENSITIVE_INTERNAL_SRAM_USAGE_LOG_SRAM")
                .WithReservedBits(1, 31);

            // SENSITIVE_CACHE_TAG_ACCESS_0_REG: 0x028
            Registers.CacheTagAccess0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CACHE_TAG_ACCESS_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CACHE_TAG_ACCESS_1_REG: 0x02C
            Registers.CacheTagAccess1.Define(this, 0x0000000F)
                .WithFlag(0, name: "SENSITIVE_PRO_I_TAG_RD_ACS")
                .WithFlag(1, name: "SENSITIVE_PRO_I_TAG_WR_ACS")
                .WithFlag(2, name: "SENSITIVE_PRO_D_TAG_RD_ACS")
                .WithFlag(3, name: "SENSITIVE_PRO_D_TAG_WR_ACS")
                .WithReservedBits(4, 28);

            // SENSITIVE_CACHE_MMU_ACCESS_0_REG: 0x030
            Registers.CacheMmuAccess0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CACHE_MMU_ACCESS_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CACHE_MMU_ACCESS_1_REG: 0x034
            Registers.CacheMmuAccess1.Define(this, 0x00000003)
                .WithFlag(0, name: "SENSITIVE_PRO_MMU_RD_ACS")
                .WithFlag(1, name: "SENSITIVE_PRO_MMU_WR_ACS")
                .WithReservedBits(2, 30);

            // SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_0_REG: 0x038
            Registers.DmaApbperiSpi2PmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_1_REG: 0x03C
            Registers.DmaApbperiSpi2PmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_SPI2_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_0_REG: 0x040
            Registers.DmaApbperiUchi0PmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_1_REG: 0x044
            Registers.DmaApbperiUchi0PmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_UCHI0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_0_REG: 0x048
            Registers.DmaApbperiI2s0PmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_1_REG: 0x04C
            Registers.DmaApbperiI2s0PmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_I2S0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_0_REG: 0x050
            Registers.DmaApbperiMacPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_1_REG: 0x054
            Registers.DmaApbperiMacPmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_MAC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_0_REG: 0x058
            Registers.DmaApbperiBackupPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_1_REG: 0x05C
            Registers.DmaApbperiBackupPmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_BACKUP_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_0_REG: 0x060
            Registers.DmaApbperiLcPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_1_REG: 0x064
            Registers.DmaApbperiLcPmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_LC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_0_REG: 0x068
            Registers.DmaApbperiAesPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_1_REG: 0x06C
            Registers.DmaApbperiAesPmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_AES_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_0_REG: 0x070
            Registers.DmaApbperiShaPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_1_REG: 0x074
            Registers.DmaApbperiShaPmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_SHA_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_0_REG: 0x078
            Registers.DmaApbperiAdcDacPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_1_REG: 0x07C
            Registers.DmaApbperiAdcDacPmsConstrain1.Define(this, 0x000FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_DMA_APBPERI_ADC_DAC_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 12);

            // SENSITIVE_DMA_APBPERI_PMS_MONITOR_0_REG: 0x080
            Registers.DmaApbperiPmsMonitor0.Define(this)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_PMS_MONITOR_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_DMA_APBPERI_PMS_MONITOR_1_REG: 0x084
            Registers.DmaApbperiPmsMonitor1.Define(this, 0x00000003)
                .WithFlag(0, name: "SENSITIVE_DMA_APBPERI_PMS_MONITOR_VIOLATE_CLR")
                .WithFlag(1, name: "SENSITIVE_DMA_APBPERI_PMS_MONITOR_VIOLATE_EN")
                .WithReservedBits(2, 30);

            // SENSITIVE_DMA_APBPERI_PMS_MONITOR_2_REG: 0x088
            Registers.DmaApbperiPmsMonitor2.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "SENSITIVE_DMA_APBPERI_PMS_MONITOR_VIOLATE_INTR")
                .WithValueField(1, 2, mode: FieldMode.Read, name: "SENSITIVE_DMA_APBPERI_PMS_MONITOR_VIOLATE_STATUS_WORLD")
                .WithValueField(3, 24, mode: FieldMode.Read, name: "SENSITIVE_DMA_APBPERI_PMS_MONITOR_VIOLATE_STATUS_ADDR")
                .WithReservedBits(27, 5);

            // SENSITIVE_DMA_APBPERI_PMS_MONITOR_3_REG: 0x08C
            Registers.DmaApbperiPmsMonitor3.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "SENSITIVE_DMA_APBPERI_PMS_MONITOR_VIOLATE_STATUS_WR")
                .WithValueField(1, 4, mode: FieldMode.Read, name: "SENSITIVE_DMA_APBPERI_PMS_MONITOR_VIOLATE_STATUS_BYTEEN")
                .WithReservedBits(5, 27);

            // SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SPLIT_LINE_CONSTRAIN_0_REG: 0x090
            Registers.CoreXIram0Dram0DmaSplitLineConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SPLIT_LINE_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SPLIT_LINE_CONSTRAIN_1_REG: 0x094
            Registers.CoreXIram0Dram0DmaSplitLineConstrain1.Define(this)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SRAM_CATEGORY_0")
                .WithValueField(2, 2, name: "SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SRAM_CATEGORY_1")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SRAM_CATEGORY_2")
                .WithReservedBits(6, 8)
                .WithValueField(14, 8, name: "SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SRAM_SPLITADDR")
                .WithReservedBits(22, 10);

            // SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SPLIT_LINE_CONSTRAIN_2_REG: 0x098
            Registers.CoreXIram0Dram0DmaSplitLineConstrain2.Define(this)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_X_IRAM0_SRAM_LINE_0_CATEGORY_0")
                .WithValueField(2, 2, name: "SENSITIVE_CORE_X_IRAM0_SRAM_LINE_0_CATEGORY_1")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_X_IRAM0_SRAM_LINE_0_CATEGORY_2")
                .WithReservedBits(6, 8)
                .WithValueField(14, 8, name: "SENSITIVE_CORE_X_IRAM0_SRAM_LINE_0_SPLITADDR")
                .WithReservedBits(22, 10);

            // SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SPLIT_LINE_CONSTRAIN_3_REG: 0x09C
            Registers.CoreXIram0Dram0DmaSplitLineConstrain3.Define(this)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_X_IRAM0_SRAM_LINE_1_CATEGORY_0")
                .WithValueField(2, 2, name: "SENSITIVE_CORE_X_IRAM0_SRAM_LINE_1_CATEGORY_1")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_X_IRAM0_SRAM_LINE_1_CATEGORY_2")
                .WithReservedBits(6, 8)
                .WithValueField(14, 8, name: "SENSITIVE_CORE_X_IRAM0_SRAM_LINE_1_SPLITADDR")
                .WithReservedBits(22, 10);

            // SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SPLIT_LINE_CONSTRAIN_4_REG: 0x0A0
            Registers.CoreXIram0Dram0DmaSplitLineConstrain4.Define(this)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_X_DRAM0_DMA_SRAM_LINE_0_CATEGORY_0")
                .WithValueField(2, 2, name: "SENSITIVE_CORE_X_DRAM0_DMA_SRAM_LINE_0_CATEGORY_1")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_X_DRAM0_DMA_SRAM_LINE_0_CATEGORY_2")
                .WithReservedBits(6, 8)
                .WithValueField(14, 8, name: "SENSITIVE_CORE_X_DRAM0_DMA_SRAM_LINE_0_SPLITADDR")
                .WithReservedBits(22, 10);

            // SENSITIVE_CORE_X_IRAM0_DRAM0_DMA_SPLIT_LINE_CONSTRAIN_5_REG: 0x0A4
            Registers.CoreXIram0Dram0DmaSplitLineConstrain5.Define(this)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_X_DRAM0_DMA_SRAM_LINE_1_CATEGORY_0")
                .WithValueField(2, 2, name: "SENSITIVE_CORE_X_DRAM0_DMA_SRAM_LINE_1_CATEGORY_1")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_X_DRAM0_DMA_SRAM_LINE_1_CATEGORY_2")
                .WithReservedBits(6, 8)
                .WithValueField(14, 8, name: "SENSITIVE_CORE_X_DRAM0_DMA_SRAM_LINE_1_SPLITADDR")
                .WithReservedBits(22, 10);

            // SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_0_REG: 0x0A8
            Registers.CoreXIram0PmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_1_REG: 0x0AC
            Registers.CoreXIram0PmsConstrain1.Define(this, 0x001C7FFF)
                .WithValueField(0, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(3, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(6, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(9, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithValueField(12, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_CACHEDATAARRAY_PMS_0")
                .WithReservedBits(15, 3)
                .WithValueField(18, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_ROM_WORLD_1_PMS")
                .WithReservedBits(21, 11);

            // SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_2_REG: 0x0B0
            Registers.CoreXIram0PmsConstrain2.Define(this, 0x001C7FFF)
                .WithValueField(0, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(3, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(6, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(9, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithValueField(12, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_CACHEDATAARRAY_PMS_0")
                .WithReservedBits(15, 3)
                .WithValueField(18, 3, name: "SENSITIVE_CORE_X_IRAM0_PMS_CONSTRAIN_ROM_WORLD_0_PMS")
                .WithReservedBits(21, 11);

            // SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_0_REG: 0x0B4
            Registers.Core0Iram0PmsMonitor0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_1_REG: 0x0B8
            Registers.Core0Iram0PmsMonitor1.Define(this, 0x00000003)
                .WithFlag(0, name: "SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_VIOLATE_CLR")
                .WithFlag(1, name: "SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_VIOLATE_EN")
                .WithReservedBits(2, 30);

            // SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_2_REG: 0x0BC
            Registers.Core0Iram0PmsMonitor2.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_VIOLATE_INTR")
                .WithFlag(1, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_VIOLATE_STATUS_WR")
                .WithFlag(2, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_VIOLATE_STATUS_LOADSTORE")
                .WithValueField(3, 2, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_VIOLATE_STATUS_WORLD")
                .WithValueField(5, 24, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_IRAM0_PMS_MONITOR_VIOLATE_STATUS_ADDR")
                .WithReservedBits(29, 3);

            // SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_0_REG: 0x0C0
            Registers.CoreXDram0PmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_1_REG: 0x0C4
            Registers.CoreXDram0PmsConstrain1.Define(this, 0x0F0FF0FF)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_0")
                .WithValueField(2, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_1")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_2")
                .WithValueField(6, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_SRAM_WORLD_0_PMS_3")
                .WithReservedBits(8, 4)
                .WithValueField(12, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_0")
                .WithValueField(14, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_1")
                .WithValueField(16, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_2")
                .WithValueField(18, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_SRAM_WORLD_1_PMS_3")
                .WithReservedBits(20, 4)
                .WithValueField(24, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_ROM_WORLD_0_PMS")
                .WithValueField(26, 2, name: "SENSITIVE_CORE_X_DRAM0_PMS_CONSTRAIN_ROM_WORLD_1_PMS")
                .WithReservedBits(28, 4);

            // SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_0_REG: 0x0C8
            Registers.Core0Dram0PmsMonitor0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_1_REG: 0x0CC
            Registers.Core0Dram0PmsMonitor1.Define(this, 0x00000003)
                .WithFlag(0, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_VIOLATE_CLR")
                .WithFlag(1, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_VIOLATE_EN")
                .WithReservedBits(2, 30);

            // SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_2_REG: 0x0D0
            Registers.Core0Dram0PmsMonitor2.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_VIOLATE_INTR")
                .WithFlag(1, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_VIOLATE_STATUS_LOCK")
                .WithValueField(2, 2, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_VIOLATE_STATUS_WORLD")
                .WithValueField(4, 24, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_VIOLATE_STATUS_ADDR")
                .WithReservedBits(28, 4);

            // SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_3_REG: 0x0D4
            Registers.Core0Dram0PmsMonitor3.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_VIOLATE_STATUS_WR")
                .WithValueField(1, 4, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_DRAM0_PMS_MONITOR_VIOLATE_STATUS_BYTEEN")
                .WithReservedBits(5, 27);

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_0_REG: 0x0D8
            Registers.Core0PifPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_1_REG: 0x0DC
            Registers.Core0PifPmsConstrain1.Define(this, 0xCF0FFFFF)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_UART")
                .WithValueField(2, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_G0SPI_1")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_G0SPI_0")
                .WithValueField(6, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_GPIO")
                .WithValueField(8, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_FE2")
                .WithValueField(10, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_FE")
                .WithValueField(12, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_TIMER")
                .WithValueField(14, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_RTC")
                .WithValueField(16, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_IO_MUX")
                .WithValueField(18, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_WDG")
                .WithReservedBits(20, 4)
                .WithValueField(24, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_MISC")
                .WithValueField(26, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_I2C")
                .WithReservedBits(28, 2)
                .WithValueField(30, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_UART1");

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_2_REG: 0x0E0
            Registers.Core0PifPmsConstrain2.Define(this, 0xFCC30CF3)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_BT")
                .WithReservedBits(2, 2)
                .WithValueField(4, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_I2C_EXT0")
                .WithValueField(6, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_UHCI0")
                .WithReservedBits(8, 2)
                .WithValueField(10, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_RMT")
                .WithReservedBits(12, 4)
                .WithValueField(16, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_LEDC")
                .WithReservedBits(18, 4)
                .WithValueField(22, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_BB")
                .WithReservedBits(24, 2)
                .WithValueField(26, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_TIMERGROUP")
                .WithValueField(28, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_TIMERGROUP1")
                .WithValueField(30, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_SYSTIMER");

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_3_REG: 0x0E4
            Registers.Core0PifPmsConstrain3.Define(this, 0x3CC0CC33)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_SPI_2")
                .WithReservedBits(2, 2)
                .WithValueField(4, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_APB_CTRL")
                .WithReservedBits(6, 4)
                .WithValueField(10, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_CAN")
                .WithReservedBits(12, 2)
                .WithValueField(14, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_I2S0")
                .WithReservedBits(16, 6)
                .WithValueField(22, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_RWBT")
                .WithReservedBits(24, 2)
                .WithValueField(26, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_WIFIMAC")
                .WithValueField(28, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_PWR")
                .WithReservedBits(30, 2);

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_4_REG: 0x0E8
            Registers.Core0PifPmsConstrain4.Define(this, 0xFFFFF3FC)
                .WithReservedBits(0, 2)
                .WithValueField(2, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_USB_WRAP")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_CRYPTO_PERI")
                .WithValueField(6, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_CRYPTO_DMA")
                .WithValueField(8, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_APB_ADC")
                .WithReservedBits(10, 2)
                .WithValueField(12, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_BT_PWR")
                .WithValueField(14, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_USB_DEVICE")
                .WithValueField(16, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_SYSTEM")
                .WithValueField(18, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_SENSITIVE")
                .WithValueField(20, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_INTERRUPT")
                .WithValueField(22, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_DMA_COPY")
                .WithValueField(24, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_CACHE_CONFIG")
                .WithValueField(26, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_AD")
                .WithValueField(28, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_DIO")
                .WithValueField(30, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_0_WORLD_CONTROLLER");

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_5_REG: 0x0EC
            Registers.Core0PifPmsConstrain5.Define(this, 0xCF0FFFFF)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_UART")
                .WithValueField(2, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_G0SPI_1")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_G0SPI_0")
                .WithValueField(6, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_GPIO")
                .WithValueField(8, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_FE2")
                .WithValueField(10, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_FE")
                .WithValueField(12, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_TIMER")
                .WithValueField(14, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_RTC")
                .WithValueField(16, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_IO_MUX")
                .WithValueField(18, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_WDG")
                .WithReservedBits(20, 4)
                .WithValueField(24, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_MISC")
                .WithValueField(26, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_I2C")
                .WithReservedBits(28, 2)
                .WithValueField(30, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_UART1");

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_6_REG: 0x0F0
            Registers.Core0PifPmsConstrain6.Define(this, 0xFCC30CF3)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_BT")
                .WithReservedBits(2, 2)
                .WithValueField(4, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_I2C_EXT0")
                .WithValueField(6, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_UHCI0")
                .WithReservedBits(8, 2)
                .WithValueField(10, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_RMT")
                .WithReservedBits(12, 4)
                .WithValueField(16, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_LEDC")
                .WithReservedBits(18, 4)
                .WithValueField(22, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_BB")
                .WithReservedBits(24, 2)
                .WithValueField(26, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_TIMERGROUP")
                .WithValueField(28, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_TIMERGROUP1")
                .WithValueField(30, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_SYSTIMER");

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_7_REG: 0x0F4
            Registers.Core0PifPmsConstrain7.Define(this, 0x3CC0CC33)
                .WithValueField(0, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_SPI_2")
                .WithReservedBits(2, 2)
                .WithValueField(4, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_APB_CTRL")
                .WithReservedBits(6, 4)
                .WithValueField(10, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_CAN")
                .WithReservedBits(12, 2)
                .WithValueField(14, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_I2S0")
                .WithReservedBits(16, 6)
                .WithValueField(22, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_RWBT")
                .WithReservedBits(24, 2)
                .WithValueField(26, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_WIFIMAC")
                .WithValueField(28, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_PWR")
                .WithReservedBits(30, 2);

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_8_REG: 0x0F8
            Registers.Core0PifPmsConstrain8.Define(this, 0xFFFFF3FC)
                .WithReservedBits(0, 2)
                .WithValueField(2, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_USB_WRAP")
                .WithValueField(4, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_CRYPTO_PERI")
                .WithValueField(6, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_CRYPTO_DMA")
                .WithValueField(8, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_APB_ADC")
                .WithReservedBits(10, 2)
                .WithValueField(12, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_BT_PWR")
                .WithValueField(14, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_USB_DEVICE")
                .WithValueField(16, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_SYSTEM")
                .WithValueField(18, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_SENSITIVE")
                .WithValueField(20, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_INTERRUPT")
                .WithValueField(22, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_DMA_COPY")
                .WithValueField(24, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_CACHE_CONFIG")
                .WithValueField(26, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_AD")
                .WithValueField(28, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_DIO")
                .WithValueField(30, 2, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_WORLD_1_WORLD_CONTROLLER");

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_9_REG: 0x0FC
            Registers.Core0PifPmsConstrain9.Define(this, 0x003FFFFF)
                .WithValueField(0, 11, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_RTCFAST_SPLTADDR_WORLD_0")
                .WithValueField(11, 11, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_RTCFAST_SPLTADDR_WORLD_1")
                .WithReservedBits(22, 10);

            // SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_10_REG: 0x100
            Registers.Core0PifPmsConstrain10.Define(this, 0x00000FFF)
                .WithValueField(0, 3, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_RTCFAST_WORLD_0_L")
                .WithValueField(3, 3, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_RTCFAST_WORLD_0_H")
                .WithValueField(6, 3, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_RTCFAST_WORLD_1_L")
                .WithValueField(9, 3, name: "SENSITIVE_CORE_0_PIF_PMS_CONSTRAIN_RTCFAST_WORLD_1_H")
                .WithReservedBits(12, 20);

            // SENSITIVE_REGION_PMS_CONSTRAIN_0_REG: 0x104
            Registers.RegionPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_REGION_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_REGION_PMS_CONSTRAIN_1_REG: 0x108
            Registers.RegionPmsConstrain1.Define(this, 0x00003FFF)
                .WithValueField(0, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_0_AREA_0")
                .WithValueField(2, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_0_AREA_1")
                .WithValueField(4, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_0_AREA_2")
                .WithValueField(6, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_0_AREA_3")
                .WithValueField(8, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_0_AREA_4")
                .WithValueField(10, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_0_AREA_5")
                .WithValueField(12, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_0_AREA_6")
                .WithReservedBits(14, 18);

            // SENSITIVE_REGION_PMS_CONSTRAIN_2_REG: 0x10C
            Registers.RegionPmsConstrain2.Define(this, 0x00003FFF)
                .WithValueField(0, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_1_AREA_0")
                .WithValueField(2, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_1_AREA_1")
                .WithValueField(4, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_1_AREA_2")
                .WithValueField(6, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_1_AREA_3")
                .WithValueField(8, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_1_AREA_4")
                .WithValueField(10, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_1_AREA_5")
                .WithValueField(12, 2, name: "SENSITIVE_REGION_PMS_CONSTRAIN_WORLD_1_AREA_6")
                .WithReservedBits(14, 18);

            // SENSITIVE_REGION_PMS_CONSTRAIN_3_REG: 0x110
            Registers.RegionPmsConstrain3.Define(this)
                .WithValueField(0, 30, name: "SENSITIVE_REGION_PMS_CONSTRAIN_ADDR_0")
                .WithReservedBits(30, 2);

            // SENSITIVE_REGION_PMS_CONSTRAIN_4_REG: 0x114
            Registers.RegionPmsConstrain4.Define(this)
                .WithValueField(0, 30, name: "SENSITIVE_REGION_PMS_CONSTRAIN_ADDR_1")
                .WithReservedBits(30, 2);

            // SENSITIVE_REGION_PMS_CONSTRAIN_5_REG: 0x118
            Registers.RegionPmsConstrain5.Define(this)
                .WithValueField(0, 30, name: "SENSITIVE_REGION_PMS_CONSTRAIN_ADDR_2")
                .WithReservedBits(30, 2);

            // SENSITIVE_REGION_PMS_CONSTRAIN_6_REG: 0x11C
            Registers.RegionPmsConstrain6.Define(this)
                .WithValueField(0, 30, name: "SENSITIVE_REGION_PMS_CONSTRAIN_ADDR_3")
                .WithReservedBits(30, 2);

            // SENSITIVE_REGION_PMS_CONSTRAIN_7_REG: 0x120
            Registers.RegionPmsConstrain7.Define(this)
                .WithValueField(0, 30, name: "SENSITIVE_REGION_PMS_CONSTRAIN_ADDR_4")
                .WithReservedBits(30, 2);

            // SENSITIVE_REGION_PMS_CONSTRAIN_8_REG: 0x124
            Registers.RegionPmsConstrain8.Define(this)
                .WithValueField(0, 30, name: "SENSITIVE_REGION_PMS_CONSTRAIN_ADDR_5")
                .WithReservedBits(30, 2);

            // SENSITIVE_REGION_PMS_CONSTRAIN_9_REG: 0x128
            Registers.RegionPmsConstrain9.Define(this)
                .WithValueField(0, 30, name: "SENSITIVE_REGION_PMS_CONSTRAIN_ADDR_6")
                .WithReservedBits(30, 2);

            // SENSITIVE_REGION_PMS_CONSTRAIN_10_REG: 0x12C
            Registers.RegionPmsConstrain10.Define(this)
                .WithValueField(0, 30, name: "SENSITIVE_REGION_PMS_CONSTRAIN_ADDR_7")
                .WithReservedBits(30, 2);

            // SENSITIVE_CORE_0_PIF_PMS_MONITOR_0_REG: 0x130
            Registers.Core0PifPmsMonitor0.Define(this)
                .WithFlag(0, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_CORE_0_PIF_PMS_MONITOR_1_REG: 0x134
            Registers.Core0PifPmsMonitor1.Define(this, 0x00000003)
                .WithFlag(0, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_VIOLATE_CLR")
                .WithFlag(1, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_VIOLATE_EN")
                .WithReservedBits(2, 30);

            // SENSITIVE_CORE_0_PIF_PMS_MONITOR_2_REG: 0x138
            Registers.Core0PifPmsMonitor2.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_VIOLATE_INTR")
                .WithFlag(1, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_VIOLATE_STATUS_HPORT_0")
                .WithValueField(2, 3, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_VIOLATE_STATUS_HSIZE")
                .WithFlag(5, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_VIOLATE_STATUS_HWRITE")
                .WithValueField(6, 2, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_VIOLATE_STATUS_HWORLD")
                .WithReservedBits(8, 24);

            // SENSITIVE_CORE_0_PIF_PMS_MONITOR_3_REG: 0x13C
            Registers.Core0PifPmsMonitor3.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_VIOLATE_STATUS_HADDR");

            // SENSITIVE_CORE_0_PIF_PMS_MONITOR_4_REG: 0x140
            Registers.Core0PifPmsMonitor4.Define(this, 0x00000003)
                .WithFlag(0, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_NONWORD_VIOLATE_CLR")
                .WithFlag(1, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_NONWORD_VIOLATE_EN")
                .WithReservedBits(2, 30);

            // SENSITIVE_CORE_0_PIF_PMS_MONITOR_5_REG: 0x144
            Registers.Core0PifPmsMonitor5.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_NONWORD_VIOLATE_INTR")
                .WithValueField(1, 2, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_NONWORD_VIOLATE_STATUS_HSIZE")
                .WithValueField(3, 2, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_NONWORD_VIOLATE_STATUS_HWORLD")
                .WithReservedBits(5, 27);

            // SENSITIVE_CORE_0_PIF_PMS_MONITOR_6_REG: 0x148
            Registers.Core0PifPmsMonitor6.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "SENSITIVE_CORE_0_PIF_PMS_MONITOR_NONWORD_VIOLATE_STATUS_HADDR");

            // SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_0_REG: 0x14C
            Registers.BackupBusPmsConstrain0.Define(this)
                .WithFlag(0, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_1_REG: 0x150
            Registers.BackupBusPmsConstrain1.Define(this, 0xCF0FFFFF)
                .WithValueField(0, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_UART")
                .WithValueField(2, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_G0SPI_1")
                .WithValueField(4, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_G0SPI_0")
                .WithValueField(6, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_GPIO")
                .WithValueField(8, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_FE2")
                .WithValueField(10, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_FE")
                .WithValueField(12, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_TIMER")
                .WithValueField(14, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_RTC")
                .WithValueField(16, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_IO_MUX")
                .WithValueField(18, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_WDG")
                .WithReservedBits(20, 4)
                .WithValueField(24, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_MISC")
                .WithValueField(26, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_I2C")
                .WithReservedBits(28, 2)
                .WithValueField(30, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_UART1");

            // SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_2_REG: 0x154
            Registers.BackupBusPmsConstrain2.Define(this, 0xFCC30CF3)
                .WithValueField(0, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_BT")
                .WithReservedBits(2, 2)
                .WithValueField(4, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_I2C_EXT0")
                .WithValueField(6, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_UHCI0")
                .WithReservedBits(8, 2)
                .WithValueField(10, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_RMT")
                .WithReservedBits(12, 4)
                .WithValueField(16, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_LEDC")
                .WithReservedBits(18, 4)
                .WithValueField(22, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_BB")
                .WithReservedBits(24, 2)
                .WithValueField(26, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_TIMERGROUP")
                .WithValueField(28, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_TIMERGROUP1")
                .WithValueField(30, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_SYSTIMER");

            // SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_3_REG: 0x158
            Registers.BackupBusPmsConstrain3.Define(this, 0x3CC0CC33)
                .WithValueField(0, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_SPI_2")
                .WithReservedBits(2, 2)
                .WithValueField(4, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_APB_CTRL")
                .WithReservedBits(6, 4)
                .WithValueField(10, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_CAN")
                .WithReservedBits(12, 2)
                .WithValueField(14, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_I2S0")
                .WithReservedBits(16, 6)
                .WithValueField(22, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_RWBT")
                .WithReservedBits(24, 2)
                .WithValueField(26, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_WIFIMAC")
                .WithValueField(28, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_PWR")
                .WithReservedBits(30, 2);

            // SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_4_REG: 0x15C
            Registers.BackupBusPmsConstrain4.Define(this, 0x0000F3FC)
                .WithReservedBits(0, 2)
                .WithValueField(2, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_USB_WRAP")
                .WithValueField(4, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_CRYPTO_PERI")
                .WithValueField(6, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_CRYPTO_DMA")
                .WithValueField(8, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_APB_ADC")
                .WithReservedBits(10, 2)
                .WithValueField(12, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_BT_PWR")
                .WithValueField(14, 2, name: "SENSITIVE_BACKUP_BUS_PMS_CONSTRAIN_USB_DEVICE")
                .WithReservedBits(16, 16);

            // SENSITIVE_BACKUP_BUS_PMS_MONITOR_0_REG: 0x160
            Registers.BackupBusPmsMonitor0.Define(this)
                .WithFlag(0, name: "SENSITIVE_BACKUP_BUS_PMS_MONITOR_LOCK")
                .WithReservedBits(1, 31);

            // SENSITIVE_BACKUP_BUS_PMS_MONITOR_1_REG: 0x164
            Registers.BackupBusPmsMonitor1.Define(this, 0x00000003)
                .WithFlag(0, name: "SENSITIVE_BACKUP_BUS_PMS_MONITOR_VIOLATE_CLR")
                .WithFlag(1, name: "SENSITIVE_BACKUP_BUS_PMS_MONITOR_VIOLATE_EN")
                .WithReservedBits(2, 30);

            // SENSITIVE_BACKUP_BUS_PMS_MONITOR_2_REG: 0x168
            Registers.BackupBusPmsMonitor2.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "SENSITIVE_BACKUP_BUS_PMS_MONITOR_VIOLATE_INTR")
                .WithValueField(1, 2, mode: FieldMode.Read, name: "SENSITIVE_BACKUP_BUS_PMS_MONITOR_VIOLATE_STATUS_HTRANS")
                .WithValueField(3, 3, mode: FieldMode.Read, name: "SENSITIVE_BACKUP_BUS_PMS_MONITOR_VIOLATE_STATUS_HSIZE")
                .WithFlag(6, mode: FieldMode.Read, name: "SENSITIVE_BACKUP_BUS_PMS_MONITOR_VIOLATE_STATUS_HWRITE")
                .WithReservedBits(7, 25);

            // SENSITIVE_BACKUP_BUS_PMS_MONITOR_3_REG: 0x16C
            Registers.BackupBusPmsMonitor3.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "SENSITIVE_BACKUP_BUS_PMS_MONITOR_VIOLATE_HADDR");

            // SENSITIVE_CLOCK_GATE_REG: 0x170
            Registers.ClockGate.Define(this, 0x00000001)
                .WithFlag(0, name: "SENSITIVE_CLK_EN")
                .WithReservedBits(1, 31);

            // SENSITIVE_DATE_REG: 0xFFC
            Registers.Date.Define(this, 0x02010200)
                .WithValueField(0, 28, name: "SENSITIVE_DATE")
                .WithReservedBits(28, 4);

        }

        private enum Registers : long
        {
            RomTableLock = 0x000,
            RomTable = 0x004,
            PrivilegeModeSelLock = 0x008,
            PrivilegeModeSel = 0x00C,
            ApbPeripheralAccess0 = 0x010,
            ApbPeripheralAccess1 = 0x014,
            InternalSramUsage0 = 0x018,
            InternalSramUsage1 = 0x01C,
            InternalSramUsage3 = 0x020,
            InternalSramUsage4 = 0x024,
            CacheTagAccess0 = 0x028,
            CacheTagAccess1 = 0x02C,
            CacheMmuAccess0 = 0x030,
            CacheMmuAccess1 = 0x034,
            DmaApbperiSpi2PmsConstrain0 = 0x038,
            DmaApbperiSpi2PmsConstrain1 = 0x03C,
            DmaApbperiUchi0PmsConstrain0 = 0x040,
            DmaApbperiUchi0PmsConstrain1 = 0x044,
            DmaApbperiI2s0PmsConstrain0 = 0x048,
            DmaApbperiI2s0PmsConstrain1 = 0x04C,
            DmaApbperiMacPmsConstrain0 = 0x050,
            DmaApbperiMacPmsConstrain1 = 0x054,
            DmaApbperiBackupPmsConstrain0 = 0x058,
            DmaApbperiBackupPmsConstrain1 = 0x05C,
            DmaApbperiLcPmsConstrain0 = 0x060,
            DmaApbperiLcPmsConstrain1 = 0x064,
            DmaApbperiAesPmsConstrain0 = 0x068,
            DmaApbperiAesPmsConstrain1 = 0x06C,
            DmaApbperiShaPmsConstrain0 = 0x070,
            DmaApbperiShaPmsConstrain1 = 0x074,
            DmaApbperiAdcDacPmsConstrain0 = 0x078,
            DmaApbperiAdcDacPmsConstrain1 = 0x07C,
            DmaApbperiPmsMonitor0 = 0x080,
            DmaApbperiPmsMonitor1 = 0x084,
            DmaApbperiPmsMonitor2 = 0x088,
            DmaApbperiPmsMonitor3 = 0x08C,
            CoreXIram0Dram0DmaSplitLineConstrain0 = 0x090,
            CoreXIram0Dram0DmaSplitLineConstrain1 = 0x094,
            CoreXIram0Dram0DmaSplitLineConstrain2 = 0x098,
            CoreXIram0Dram0DmaSplitLineConstrain3 = 0x09C,
            CoreXIram0Dram0DmaSplitLineConstrain4 = 0x0A0,
            CoreXIram0Dram0DmaSplitLineConstrain5 = 0x0A4,
            CoreXIram0PmsConstrain0 = 0x0A8,
            CoreXIram0PmsConstrain1 = 0x0AC,
            CoreXIram0PmsConstrain2 = 0x0B0,
            Core0Iram0PmsMonitor0 = 0x0B4,
            Core0Iram0PmsMonitor1 = 0x0B8,
            Core0Iram0PmsMonitor2 = 0x0BC,
            CoreXDram0PmsConstrain0 = 0x0C0,
            CoreXDram0PmsConstrain1 = 0x0C4,
            Core0Dram0PmsMonitor0 = 0x0C8,
            Core0Dram0PmsMonitor1 = 0x0CC,
            Core0Dram0PmsMonitor2 = 0x0D0,
            Core0Dram0PmsMonitor3 = 0x0D4,
            Core0PifPmsConstrain0 = 0x0D8,
            Core0PifPmsConstrain1 = 0x0DC,
            Core0PifPmsConstrain2 = 0x0E0,
            Core0PifPmsConstrain3 = 0x0E4,
            Core0PifPmsConstrain4 = 0x0E8,
            Core0PifPmsConstrain5 = 0x0EC,
            Core0PifPmsConstrain6 = 0x0F0,
            Core0PifPmsConstrain7 = 0x0F4,
            Core0PifPmsConstrain8 = 0x0F8,
            Core0PifPmsConstrain9 = 0x0FC,
            Core0PifPmsConstrain10 = 0x100,
            RegionPmsConstrain0 = 0x104,
            RegionPmsConstrain1 = 0x108,
            RegionPmsConstrain2 = 0x10C,
            RegionPmsConstrain3 = 0x110,
            RegionPmsConstrain4 = 0x114,
            RegionPmsConstrain5 = 0x118,
            RegionPmsConstrain6 = 0x11C,
            RegionPmsConstrain7 = 0x120,
            RegionPmsConstrain8 = 0x124,
            RegionPmsConstrain9 = 0x128,
            RegionPmsConstrain10 = 0x12C,
            Core0PifPmsMonitor0 = 0x130,
            Core0PifPmsMonitor1 = 0x134,
            Core0PifPmsMonitor2 = 0x138,
            Core0PifPmsMonitor3 = 0x13C,
            Core0PifPmsMonitor4 = 0x140,
            Core0PifPmsMonitor5 = 0x144,
            Core0PifPmsMonitor6 = 0x148,
            BackupBusPmsConstrain0 = 0x14C,
            BackupBusPmsConstrain1 = 0x150,
            BackupBusPmsConstrain2 = 0x154,
            BackupBusPmsConstrain3 = 0x158,
            BackupBusPmsConstrain4 = 0x15C,
            BackupBusPmsMonitor0 = 0x160,
            BackupBusPmsMonitor1 = 0x164,
            BackupBusPmsMonitor2 = 0x168,
            BackupBusPmsMonitor3 = 0x16C,
            ClockGate = 0x170,
            Date = 0xFFC,
        }
    }
}