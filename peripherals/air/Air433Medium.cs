// Air433Medium.cs -- a shared virtual 433 MHz "air" medium for the ESP32-C3
// Renode emulation. It lets a radio-model frame reach every receiving radio on
// the medium without any RF/PHY simulation: it is a functional delivery bus, not
// a modulation/timing model.
//
// Model
//   * Radios that can receive off the air implement IAir433Node (a single
//     ReceiveAirFrame(byte[]) callback) and join the medium via their optional
//     `medium:` constructor argument (set in the .repl).
//   * A frame put on the medium is delivered synchronously to every registered
//     node except the sender. sender == null models a synthetic external
//     transmitter (e.g. a weather station), which is what InjectFrame provides.
//   * A radio TX path can call Transmit(frame, this) so a second radio on the
//     same medium receives it (radio-to-radio); the primary use here is the RX
//     path: a test injects a known frame and the receiving radio raises it to
//     the firmware.
//
// It is registered on the sysbus at a small, otherwise-unused window purely so
// the monitor can address it as `air` and a test can inject a frame; the medium
// exposes no functional registers (reads return 0, writes are ignored).
//
// Attach it and give each radio the reference via its `medium:` ctor argument:
//
//     air: Wireless.Air433Medium @ sysbus 0x60030000
//
//     radio: SPI.CC1101 @ spi2
//         medium: air
//         0 -> gpio@4
//         2 -> gpio@5
//
// Then inject a frame from a test (Renode monitor / Robot):
//
//     air InjectFrame "A7 11 22 33 44 55"
//
// The class lives in namespace Antmicro.Renode.Peripherals.Wireless so the .repl
// type resolver (which only searches Antmicro.Renode.Peripherals.*) finds it as
// Wireless.Air433Medium.

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Wireless
{
    // A radio attached to the shared 433 MHz medium that can receive frames.
    public interface IAir433Node : IPeripheral
    {
        void ReceiveAirFrame(byte[] data);
    }

    public class Air433Medium : IDoubleWordPeripheral, IKnownSize
    {
        public Air433Medium()
        {
            nodes = new List<IAir433Node>();
        }

        public void Reset()
        {
            // The medium itself is stateless; registrations survive a machine
            // Reset (they are established by the .repl at construction time).
        }

        // No functional MMIO: the sysbus window exists only so the monitor can
        // address the medium by name to inject frames.
        public uint ReadDoubleWord(long offset)
        {
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
        }

        public long Size => 0x10;

        public void RegisterNode(IAir433Node node)
        {
            if(node == null || nodes.Contains(node))
            {
                return;
            }
            nodes.Add(node);
            this.Log(LogLevel.Debug, "Air433: node {0} joined the medium ({1} total)", node, nodes.Count);
        }

        public void UnregisterNode(IAir433Node node)
        {
            nodes.Remove(node);
        }

        // Deliver a frame from sender to every other node on the medium.
        // sender == null models a synthetic external transmitter.
        public void Transmit(byte[] data, IAir433Node sender)
        {
            if(data == null || data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Air433: empty frame dropped");
                return;
            }
            var delivered = 0;
            // Snapshot so a receiver that (un)registers during delivery is safe.
            foreach(var node in nodes.ToArray())
            {
                if(ReferenceEquals(node, sender))
                {
                    continue;
                }
                node.ReceiveAirFrame(data);
                delivered++;
            }
            this.Log(LogLevel.Info, "Air433: frame of {0} byte(s) delivered to {1} receiver(s)",
                data.Length, delivered);
        }

        // Monitor-callable synthetic transmitter. Accepts a space/comma-separated
        // list of hex bytes, e.g.  air InjectFrame "A7 11 22 33 44 55".
        public void InjectFrame(string hexBytes)
        {
            Transmit(ParseHex(hexBytes), null);
        }

        private static byte[] ParseHex(string s)
        {
            if(string.IsNullOrWhiteSpace(s))
            {
                return new byte[0];
            }
            var tokens = s.Split(new[] { (char)32, (char)44, (char)9, (char)59, (char)58 },
                StringSplitOptions.RemoveEmptyEntries);
            var result = new byte[tokens.Length];
            for(var i = 0; i < tokens.Length; i++)
            {
                var tok = tokens[i];
                if(tok.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    tok = tok.Substring(2);
                }
                result[i] = Convert.ToByte(tok, 16);
            }
            return result;
        }

        private readonly List<IAir433Node> nodes;
    }
}
