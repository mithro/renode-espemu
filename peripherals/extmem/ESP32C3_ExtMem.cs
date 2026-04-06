// ESP32-C3 External Memory (Cache/MMU) Controller for Renode
//
// Controls the instruction cache (ICache) which maps flash memory into the
// CPU address space. The firmware enables ICache during boot and performs
// sync/preload operations.
//
// Base: DR_REG_EXTMEM_BASE = 0x600C4000, Size: 0x1000
//
// Boot-critical registers:
//   0x00: ICACHE_CTRL (bit 0 = ICACHE_ENABLE, must return 1)
//   0x04: ICACHE_CTRL1 (IBUS/DBUS shut control)
//   0x08: ICACHE_TAG_POWER_CTRL (tag memory power)
//   0x28: ICACHE_SYNC_CTRL (bit 1 = sync done, must return done)
//   0x34: ICACHE_PRELOAD_CTRL (bit 1 = preload done, must return done)
//   0x40: ICACHE_AUTOLOAD_CTRL (bit 3 = autoload done)
//   0xB0: CACHE_STATE (idle state indicator)
//   0xC8: CACHE_CONF_MISC (misc cache config)

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ESP32C3_ExtMem : BasicDoubleWordPeripheral, IKnownSize
    {
        public ESP32C3_ExtMem(Machine machine) : base(machine)
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
            // ICACHE_CTRL_REG: 0x00
            // bit 0 = ICACHE_ENABLE (default 0, firmware sets to 1; we default 1 for boot)
            Registers.ICacheCtrl.Define(this, 0x00000001)
                .WithFlag(0, name: "ICACHE_ENABLE")
                .WithReservedBits(1, 31);

            // ICACHE_CTRL1_REG: 0x04
            // bit 0 = ICACHE_SHUT_IBUS (default 1), bit 1 = ICACHE_SHUT_DBUS (default 1)
            Registers.ICacheCtrl1.Define(this, 0x00000003)
                .WithFlag(0, name: "ICACHE_SHUT_IBUS")
                .WithFlag(1, name: "ICACHE_SHUT_DBUS")
                .WithReservedBits(2, 30);

            // ICACHE_TAG_POWER_CTRL_REG: 0x08
            // bit 0 = TAG_MEM_FORCE_ON (default 1), bit 1 = TAG_MEM_FORCE_PD (default 0),
            // bit 2 = TAG_MEM_FORCE_PU (default 1)
            Registers.ICacheTagPowerCtrl.Define(this, 0x00000005)
                .WithFlag(0, name: "ICACHE_TAG_MEM_FORCE_ON")
                .WithFlag(1, name: "ICACHE_TAG_MEM_FORCE_PD")
                .WithFlag(2, name: "ICACHE_TAG_MEM_FORCE_PU")
                .WithReservedBits(3, 29);

            // ICACHE_PRELOCK_CTRL_REG: 0x0C
            Registers.ICachePrelockCtrl.Define(this)
                .WithFlag(0, name: "ICACHE_PRELOCK_SCT0_EN")
                .WithFlag(1, name: "ICACHE_PRELOCK_SCT1_EN")
                .WithReservedBits(2, 30);

            // ICACHE_PRELOCK_SCT0_ADDR_REG: 0x10
            Registers.ICachePrelockSct0Addr.Define(this)
                .WithValueField(0, 32, name: "ICACHE_PRELOCK_SCT0_ADDR");

            // ICACHE_PRELOCK_SCT1_ADDR_REG: 0x14
            Registers.ICachePrelockSct1Addr.Define(this)
                .WithValueField(0, 32, name: "ICACHE_PRELOCK_SCT1_ADDR");

            // ICACHE_PRELOCK_SCT_SIZE_REG: 0x18
            Registers.ICachePrelockSctSize.Define(this)
                .WithValueField(0, 16, name: "ICACHE_PRELOCK_SCT0_SIZE")
                .WithValueField(16, 16, name: "ICACHE_PRELOCK_SCT1_SIZE");

            // ICACHE_LOCK_CTRL_REG: 0x1C
            // bit 0 = LOCK_ENA, bit 1 = UNLOCK_ENA, bit 2 = LOCK_DONE (RO, default 1)
            Registers.ICacheLockCtrl.Define(this, 0x00000004)
                .WithFlag(0, name: "ICACHE_LOCK_ENA",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            this.Log(LogLevel.Debug, "ICache lock triggered");
                    })
                .WithFlag(1, name: "ICACHE_UNLOCK_ENA",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            this.Log(LogLevel.Debug, "ICache unlock triggered");
                    })
                .WithFlag(2, mode: FieldMode.Read, name: "ICACHE_LOCK_DONE",
                    valueProviderCallback: _ => true)
                .WithReservedBits(3, 29);

            // ICACHE_LOCK_ADDR_REG: 0x20
            Registers.ICacheLockAddr.Define(this)
                .WithValueField(0, 32, name: "ICACHE_LOCK_ADDR");

            // ICACHE_LOCK_SIZE_REG: 0x24
            Registers.ICacheLockSize.Define(this)
                .WithValueField(0, 16, name: "ICACHE_LOCK_SIZE")
                .WithReservedBits(16, 16);

            // ICACHE_SYNC_CTRL_REG: 0x28
            // bit 0 = INVALIDATE_ENA (default 1), bit 1 = SYNC_DONE (RO, default 0, returns 1 = always done)
            Registers.ICacheSyncCtrl.Define(this, 0x00000001)
                .WithFlag(0, name: "ICACHE_INVALIDATE_ENA",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            this.Log(LogLevel.Debug, "ICache invalidate triggered");
                    })
                .WithFlag(1, mode: FieldMode.Read, name: "ICACHE_SYNC_DONE",
                    valueProviderCallback: _ => true)
                .WithReservedBits(2, 30);

            // ICACHE_SYNC_ADDR_REG: 0x2C
            Registers.ICacheSyncAddr.Define(this)
                .WithValueField(0, 32, name: "ICACHE_SYNC_ADDR");

            // ICACHE_SYNC_SIZE_REG: 0x30
            Registers.ICacheSyncSize.Define(this)
                .WithValueField(0, 23, name: "ICACHE_SYNC_SIZE")
                .WithReservedBits(23, 9);

            // ICACHE_PRELOAD_CTRL_REG: 0x34
            // bit 0 = PRELOAD_ENA, bit 1 = PRELOAD_DONE (RO, default 1),
            // bit 2 = PRELOAD_ORDER
            Registers.ICachePreloadCtrl.Define(this, 0x00000002)
                .WithFlag(0, name: "ICACHE_PRELOAD_ENA",
                    writeCallback: (_, value) =>
                    {
                        if (value)
                            this.Log(LogLevel.Debug, "ICache preload triggered");
                    })
                .WithFlag(1, mode: FieldMode.Read, name: "ICACHE_PRELOAD_DONE",
                    valueProviderCallback: _ => true)
                .WithFlag(2, name: "ICACHE_PRELOAD_ORDER")
                .WithReservedBits(3, 29);

            // ICACHE_PRELOAD_ADDR_REG: 0x38
            Registers.ICachePreloadAddr.Define(this)
                .WithValueField(0, 32, name: "ICACHE_PRELOAD_ADDR");

            // ICACHE_PRELOAD_SIZE_REG: 0x3C
            Registers.ICachePreloadSize.Define(this)
                .WithValueField(0, 16, name: "ICACHE_PRELOAD_SIZE")
                .WithReservedBits(16, 16);

            // ICACHE_AUTOLOAD_CTRL_REG: 0x40
            // bit 0 = SCT0_ENA, bit 1 = SCT1_ENA, bit 2 = AUTOLOAD_ENA,
            // bit 3 = AUTOLOAD_DONE (RO, default 1), bit 4 = ORDER, bits [6:5] = RQST
            Registers.ICacheAutoloadCtrl.Define(this, 0x00000008)
                .WithFlag(0, name: "ICACHE_AUTOLOAD_SCT0_ENA")
                .WithFlag(1, name: "ICACHE_AUTOLOAD_SCT1_ENA")
                .WithFlag(2, name: "ICACHE_AUTOLOAD_ENA")
                .WithFlag(3, mode: FieldMode.Read, name: "ICACHE_AUTOLOAD_DONE",
                    valueProviderCallback: _ => true)
                .WithFlag(4, name: "ICACHE_AUTOLOAD_ORDER")
                .WithValueField(5, 2, name: "ICACHE_AUTOLOAD_RQST")
                .WithReservedBits(7, 25);

            // ICACHE_AUTOLOAD_SCT0_ADDR_REG: 0x44
            Registers.ICacheAutoloadSct0Addr.Define(this)
                .WithValueField(0, 32, name: "ICACHE_AUTOLOAD_SCT0_ADDR");

            // ICACHE_AUTOLOAD_SCT0_SIZE_REG: 0x48
            Registers.ICacheAutoloadSct0Size.Define(this)
                .WithValueField(0, 32, name: "ICACHE_AUTOLOAD_SCT0_SIZE");

            // ICACHE_AUTOLOAD_SCT1_ADDR_REG: 0x4C
            Registers.ICacheAutoloadSct1Addr.Define(this)
                .WithValueField(0, 32, name: "ICACHE_AUTOLOAD_SCT1_ADDR");

            // ICACHE_AUTOLOAD_SCT1_SIZE_REG: 0x50
            Registers.ICacheAutoloadSct1Size.Define(this)
                .WithValueField(0, 32, name: "ICACHE_AUTOLOAD_SCT1_SIZE");

            // IBUS_TO_FLASH_START_VADDR_REG: 0x54
            Registers.IBusToFlashStartVaddr.Define(this)
                .WithValueField(0, 32, name: "IBUS_TO_FLASH_START_VADDR");

            // IBUS_TO_FLASH_END_VADDR_REG: 0x58
            Registers.IBusToFlashEndVaddr.Define(this)
                .WithValueField(0, 32, name: "IBUS_TO_FLASH_END_VADDR");

            // DBUS_TO_FLASH_START_VADDR_REG: 0x5C
            Registers.DBusToFlashStartVaddr.Define(this)
                .WithValueField(0, 32, name: "DBUS_TO_FLASH_START_VADDR");

            // DBUS_TO_FLASH_END_VADDR_REG: 0x60
            Registers.DBusToFlashEndVaddr.Define(this)
                .WithValueField(0, 32, name: "DBUS_TO_FLASH_END_VADDR");

            // CACHE_ACS_CNT_CLR_REG: 0x64
            Registers.CacheAcsCntClr.Define(this)
                .WithFlag(0, name: "IBUS_ACS_CNT_CLR")
                .WithFlag(1, name: "DBUS_ACS_CNT_CLR")
                .WithReservedBits(2, 30);

            // IBUS_ACS_MISS_CNT_REG: 0x68 (RO)
            Registers.IBusAcsMissCnt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "IBUS_ACS_MISS_CNT");

            // IBUS_ACS_CNT_REG: 0x6C (RO)
            Registers.IBusAcsCnt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "IBUS_ACS_CNT");

            // DBUS_ACS_FLASH_MISS_CNT_REG: 0x70 (RO)
            Registers.DBusAcsFlashMissCnt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "DBUS_ACS_FLASH_MISS_CNT");

            // DBUS_ACS_CNT_REG: 0x74 (RO)
            Registers.DBusAcsCnt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "DBUS_ACS_CNT");

            // CACHE_ILG_INT_ENA_REG: 0x78
            Registers.CacheIlgIntEna.Define(this)
                .WithValueField(0, 32, name: "CACHE_ILG_INT_ENA");

            // CACHE_ILG_INT_CLR_REG: 0x7C
            Registers.CacheIlgIntClr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "CACHE_ILG_INT_CLR");

            // CACHE_ILG_INT_ST_REG: 0x80 (RO)
            Registers.CacheIlgIntSt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CACHE_ILG_INT_ST");

            // CORE0_ACS_CACHE_INT_ENA_REG: 0x84
            Registers.Core0AcsCacheIntEna.Define(this)
                .WithValueField(0, 32, name: "CORE0_ACS_CACHE_INT_ENA");

            // CORE0_ACS_CACHE_INT_CLR_REG: 0x88
            Registers.Core0AcsCacheIntClr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "CORE0_ACS_CACHE_INT_CLR");

            // CORE0_ACS_CACHE_INT_ST_REG: 0x8C (RO)
            Registers.Core0AcsCacheIntSt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE0_ACS_CACHE_INT_ST");

            // CORE0_DBUS_REJECT_ST_REG: 0x90 (RO)
            Registers.Core0DBusRejectSt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE0_DBUS_REJECT_ST");

            // CORE0_DBUS_REJECT_VADDR_REG: 0x94 (RO)
            Registers.Core0DBusRejectVaddr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE0_DBUS_REJECT_VADDR");

            // CORE0_IBUS_REJECT_ST_REG: 0x98 (RO)
            Registers.Core0IBusRejectSt.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE0_IBUS_REJECT_ST");

            // CORE0_IBUS_REJECT_VADDR_REG: 0x9C (RO)
            Registers.Core0IBusRejectVaddr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CORE0_IBUS_REJECT_VADDR");

            // CACHE_MMU_FAULT_CONTENT_REG: 0xA0 (RO)
            // Bits [9:0] = FAULT_CONTENT, [13:10] = FAULT_CODE (4-bit)
            Registers.CacheMmuFaultContent.Define(this)
                .WithValueField(0, 10, mode: FieldMode.Read, name: "CACHE_MMU_FAULT_CONTENT")
                .WithValueField(10, 4, mode: FieldMode.Read, name: "CACHE_MMU_FAULT_CODE")
                .WithReservedBits(14, 18);

            // CACHE_MMU_FAULT_VADDR_REG: 0xA4 (RO)
            Registers.CacheMmuFaultVaddr.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "CACHE_MMU_FAULT_VADDR");

            // CACHE_WRAP_AROUND_CTRL_REG: 0xA8
            Registers.CacheWrapAroundCtrl.Define(this)
                .WithFlag(0, name: "CACHE_FLASH_WRAP_AROUND")
                .WithReservedBits(1, 31);

            // CACHE_MMU_POWER_CTRL_REG: 0xAC
            // bit 0 = MMU_MEM_FORCE_ON (default 1), bit 1 = MMU_MEM_FORCE_PD (default 0),
            // bit 2 = MMU_MEM_FORCE_PU (default 1)
            Registers.CacheMmuPowerCtrl.Define(this, 0x00000005)
                .WithFlag(0, name: "CACHE_MMU_MEM_FORCE_ON")
                .WithFlag(1, name: "CACHE_MMU_MEM_FORCE_PD")
                .WithFlag(2, name: "CACHE_MMU_MEM_FORCE_PU")
                .WithReservedBits(3, 29);

            // CACHE_STATE_REG: 0xB0 (RO)
            // bits [11:0] = ICACHE_STATE (0 = idle, hardware shows 0 after boot)
            Registers.CacheState.Define(this, 0x00000000)
                .WithValueField(0, 12, mode: FieldMode.Read, name: "ICACHE_STATE",
                    valueProviderCallback: _ => 0x000)
                .WithReservedBits(12, 20);

            // CACHE_ENCRYPT_DECRYPT_RECORD_DISABLE_REG: 0xB4
            Registers.CacheEncryptDecryptRecordDisable.Define(this)
                .WithFlag(0, name: "RECORD_DISABLE_DB_ENCRYPT")
                .WithFlag(1, name: "RECORD_DISABLE_G0CB_DECRYPT")
                .WithReservedBits(2, 30);

            // CACHE_ENCRYPT_DECRYPT_CLK_FORCE_ON_REG: 0xB8
            // All bits default 1 (clock gating closed)
            Registers.CacheEncryptDecryptClkForceOn.Define(this, 0x00000007)
                .WithFlag(0, name: "CLK_FORCE_ON_MANUAL_CRYPT")
                .WithFlag(1, name: "CLK_FORCE_ON_AUTO_CRYPT")
                .WithFlag(2, name: "CLK_FORCE_ON_CRYPT")
                .WithReservedBits(3, 29);

            // CACHE_PRELOAD_INT_CTRL_REG: 0xBC
            Registers.CachePreloadIntCtrl.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "ICACHE_PRELOAD_INT_ST")
                .WithFlag(1, name: "ICACHE_PRELOAD_INT_ENA")
                .WithFlag(2, mode: FieldMode.Write, name: "ICACHE_PRELOAD_INT_CLR")
                .WithReservedBits(3, 29);

            // CACHE_SYNC_INT_CTRL_REG: 0xC0
            Registers.CacheSyncIntCtrl.Define(this)
                .WithFlag(0, mode: FieldMode.Read, name: "ICACHE_SYNC_INT_ST")
                .WithFlag(1, name: "ICACHE_SYNC_INT_ENA")
                .WithFlag(2, mode: FieldMode.Write, name: "ICACHE_SYNC_INT_CLR")
                .WithReservedBits(3, 29);

            // CACHE_MMU_OWNER_REG: 0xC4
            Registers.CacheMmuOwner.Define(this)
                .WithValueField(0, 4, name: "CACHE_MMU_OWNER")
                .WithReservedBits(4, 28);

            // CACHE_CONF_MISC_REG: 0xC8
            // bit 0 = IGNORE_PRELOAD_MMU_ENTRY_FAULT (default 1)
            // bit 1 = IGNORE_SYNC_MMU_ENTRY_FAULT (default 1)
            // bit 2 = CACHE_TRACE_ENA (default 1)
            Registers.CacheConfMisc.Define(this, 0x00000007)
                .WithFlag(0, name: "CACHE_IGNORE_PRELOAD_MMU_ENTRY_FAULT")
                .WithFlag(1, name: "CACHE_IGNORE_SYNC_MMU_ENTRY_FAULT")
                .WithFlag(2, name: "CACHE_TRACE_ENA")
                .WithReservedBits(3, 29);

            // ICACHE_FREEZE_REG: 0xCC
            // bit 0 = FREEZE_ENA, bit 1 = FREEZE_MODE, bit 2 = FREEZE_DONE (RO)
            // ROM Cache_Freeze_ICache_Enable spins on FREEZE_DONE after setting ENA.
            Registers.ICacheFreeze.Define(this)
                .WithFlag(0, name: "ICACHE_FREEZE_ENA",
                    writeCallback: (_, val) => icacheFreezeEna = val,
                    valueProviderCallback: _ => icacheFreezeEna)
                .WithFlag(1, name: "ICACHE_FREEZE_MODE")
                .WithFlag(2, mode: FieldMode.Read, name: "ICACHE_FREEZE_DONE",
                    valueProviderCallback: _ => icacheFreezeEna)
                .WithReservedBits(3, 29);

            // ICACHE_ATOMIC_OPERATE_ENA_REG: 0xD0
            // bit 0 = ATOMIC_OPERATE_ENA (default 1)
            Registers.ICacheAtomicOperateEna.Define(this, 0x00000001)
                .WithFlag(0, name: "ICACHE_ATOMIC_OPERATE_ENA")
                .WithReservedBits(1, 31);

            // CACHE_REQUEST_REG: 0xD4
            Registers.CacheRequest.Define(this)
                .WithFlag(0, name: "CACHE_REQUEST_BYPASS")
                .WithReservedBits(1, 31);

            // IBUS_PMS_TBL_LOCK_REG: 0xD8
            Registers.IBusPmsTblLock.Define(this)
                .WithFlag(0, name: "IBUS_PMS_LOCK")
                .WithReservedBits(1, 31);

            // IBUS_PMS_TBL_BOUNDARY0_REG: 0xDC
            Registers.IBusPmsTblBoundary0.Define(this)
                .WithValueField(0, 12, name: "IBUS_PMS_BOUNDARY0")
                .WithReservedBits(12, 20);

            // IBUS_PMS_TBL_BOUNDARY1_REG: 0xE0 (default 0x800)
            Registers.IBusPmsTblBoundary1.Define(this, 0x00000800)
                .WithValueField(0, 12, name: "IBUS_PMS_BOUNDARY1")
                .WithReservedBits(12, 20);

            // IBUS_PMS_TBL_BOUNDARY2_REG: 0xE4 (default 0x800)
            Registers.IBusPmsTblBoundary2.Define(this, 0x00000800)
                .WithValueField(0, 12, name: "IBUS_PMS_BOUNDARY2")
                .WithReservedBits(12, 20);

            // IBUS_PMS_TBL_ATTR_REG: 0xE8
            // bits [3:0] = SCT1_ATTR (default 0xF), bits [7:4] = SCT2_ATTR (default 0xF)
            Registers.IBusPmsTblAttr.Define(this, 0x000000FF)
                .WithValueField(0, 4, name: "IBUS_PMS_SCT1_ATTR")
                .WithValueField(4, 4, name: "IBUS_PMS_SCT2_ATTR")
                .WithReservedBits(8, 24);

            // DBUS_PMS_TBL_LOCK_REG: 0xEC
            Registers.DBusPmsTblLock.Define(this)
                .WithFlag(0, name: "DBUS_PMS_LOCK")
                .WithReservedBits(1, 31);

            // DBUS_PMS_TBL_BOUNDARY0_REG: 0xF0
            Registers.DBusPmsTblBoundary0.Define(this)
                .WithValueField(0, 12, name: "DBUS_PMS_BOUNDARY0")
                .WithReservedBits(12, 20);

            // DBUS_PMS_TBL_BOUNDARY1_REG: 0xF4 (default 0x800)
            Registers.DBusPmsTblBoundary1.Define(this, 0x00000800)
                .WithValueField(0, 12, name: "DBUS_PMS_BOUNDARY1")
                .WithReservedBits(12, 20);

            // DBUS_PMS_TBL_BOUNDARY2_REG: 0xF8 (default 0x800)
            Registers.DBusPmsTblBoundary2.Define(this, 0x00000800)
                .WithValueField(0, 12, name: "DBUS_PMS_BOUNDARY2")
                .WithReservedBits(12, 20);

            // DBUS_PMS_TBL_ATTR_REG: 0xFC
            // bits [1:0] = SCT1_ATTR (default 0x3), bits [3:2] = SCT2_ATTR (default 0x3)
            Registers.DBusPmsTblAttr.Define(this, 0x0000000F)
                .WithValueField(0, 2, name: "DBUS_PMS_SCT1_ATTR")
                .WithValueField(2, 2, name: "DBUS_PMS_SCT2_ATTR")
                .WithReservedBits(4, 28);

            // CLOCK_GATE_REG: 0x100
            // bit 0 = CLK_EN (default 1)
            Registers.ClockGate.Define(this, 0x00000001)
                .WithFlag(0, name: "CLK_EN")
                .WithReservedBits(1, 31);

            // DATE_REG: 0x3FC
            Registers.Date.Define(this, 0x02007160)
                .WithValueField(0, 28, name: "DATE")
                .WithReservedBits(28, 4);
        }

        private enum Registers : long
        {
            ICacheCtrl = 0x00,
            ICacheCtrl1 = 0x04,
            ICacheTagPowerCtrl = 0x08,
            ICachePrelockCtrl = 0x0C,
            ICachePrelockSct0Addr = 0x10,
            ICachePrelockSct1Addr = 0x14,
            ICachePrelockSctSize = 0x18,
            ICacheLockCtrl = 0x1C,
            ICacheLockAddr = 0x20,
            ICacheLockSize = 0x24,
            ICacheSyncCtrl = 0x28,
            ICacheSyncAddr = 0x2C,
            ICacheSyncSize = 0x30,
            ICachePreloadCtrl = 0x34,
            ICachePreloadAddr = 0x38,
            ICachePreloadSize = 0x3C,
            ICacheAutoloadCtrl = 0x40,
            ICacheAutoloadSct0Addr = 0x44,
            ICacheAutoloadSct0Size = 0x48,
            ICacheAutoloadSct1Addr = 0x4C,
            ICacheAutoloadSct1Size = 0x50,
            IBusToFlashStartVaddr = 0x54,
            IBusToFlashEndVaddr = 0x58,
            DBusToFlashStartVaddr = 0x5C,
            DBusToFlashEndVaddr = 0x60,
            CacheAcsCntClr = 0x64,
            IBusAcsMissCnt = 0x68,
            IBusAcsCnt = 0x6C,
            DBusAcsFlashMissCnt = 0x70,
            DBusAcsCnt = 0x74,
            CacheIlgIntEna = 0x78,
            CacheIlgIntClr = 0x7C,
            CacheIlgIntSt = 0x80,
            Core0AcsCacheIntEna = 0x84,
            Core0AcsCacheIntClr = 0x88,
            Core0AcsCacheIntSt = 0x8C,
            Core0DBusRejectSt = 0x90,
            Core0DBusRejectVaddr = 0x94,
            Core0IBusRejectSt = 0x98,
            Core0IBusRejectVaddr = 0x9C,
            CacheMmuFaultContent = 0xA0,
            CacheMmuFaultVaddr = 0xA4,
            CacheWrapAroundCtrl = 0xA8,
            CacheMmuPowerCtrl = 0xAC,
            CacheState = 0xB0,
            CacheEncryptDecryptRecordDisable = 0xB4,
            CacheEncryptDecryptClkForceOn = 0xB8,
            CachePreloadIntCtrl = 0xBC,
            CacheSyncIntCtrl = 0xC0,
            CacheMmuOwner = 0xC4,
            CacheConfMisc = 0xC8,
            ICacheFreeze = 0xCC,
            ICacheAtomicOperateEna = 0xD0,
            CacheRequest = 0xD4,
            IBusPmsTblLock = 0xD8,
            IBusPmsTblBoundary0 = 0xDC,
            IBusPmsTblBoundary1 = 0xE0,
            IBusPmsTblBoundary2 = 0xE4,
            IBusPmsTblAttr = 0xE8,
            DBusPmsTblLock = 0xEC,
            DBusPmsTblBoundary0 = 0xF0,
            DBusPmsTblBoundary1 = 0xF4,
            DBusPmsTblBoundary2 = 0xF8,
            DBusPmsTblAttr = 0xFC,
            ClockGate = 0x100,
            Date = 0x3FC,
        }

        private bool icacheFreezeEna;
    }
}
