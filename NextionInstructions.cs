using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NC.Nextion
{
    public static class NextionCommands
    {
        /// <summary>
        /// Change page to page specified. Unloads old page to load specified page.
        /// Nextion loads page 0 by default on power on.
        /// </summary>
        /// <param name="pid"> is either the page index number, or pagename</param>
        /// <returns></returns>
        public static string Page( string pid)
        {
            return $"page {pid}";
        }

        /// <summary>
        /// Refresh component (auto-refresh when attribute changes since v0.38) - if component is obstructed(stacking), ref brings component to top.
        /// </summary>
        /// <param name="cid">is component’s .id or .objname attribute of component to refresh– when<cid> is 0 (page component) refreshes all on the current page.</param>
        /// <returns></returns>
        public static string Ref(string cid)
        {
            return $"ref {cid}";
        }

        /// <summary>
        /// Trigger the specified components Touch Press/Release Event
        /// As event code is always local, object can not be page prefixed
        /// </summary>
        /// <param name="cid">component’s .id or .objname attribute of component to refresh</param>
        /// <param name="triggerRelease">false to trigger Press Event, true to trigger Release Events</param>
        /// <returns></returns>
        public static string Click(string cid, bool triggerRelease = false)
        {
            return $"click {cid},{(triggerRelease ? 0 : 1)}";
        }

        /// <summary>
        /// Sets the backlight level in percent
        /// min 0, max 100, default 100 or user defined
        /// </summary>
        public static string Dim(int brightnessPercent, bool save = false)
        {
            return $"dim{(save ? "s" : "")}={brightnessPercent}";
        }

        public static int[] BaudRates = new int [] { 921600, 115200, 9600, 2400, 4800, 19200, 31250, 38400, 57600, 230400, 250000, 256000, 512000 };

        /// <summary>
        /// Set Baud Rate
        /// min 0, max 100, default 100 or user defined
        /// </summary>
        public static string Baud(int baud, bool save = false)
        {
            if (BaudRates.Contains(baud) == false)
            {
                throw new ArgumentException("Invalid Baud Rate");
            }

            return $"baud{(save ? "s" : "")}={baud}";
        }

        /// <summary>
        /// Turns the internal touch drawing function on or off.
        /// </summary>
        public static string TouchDrawing(bool enable = true)
        {
            return $"thdra={(enable ? "1" : "0")}";
        }

        /// <summary>
        /// When the drawing function is on, Nextion will follow touch dragging with the current brush color (as determined by the thc variable).
        /// </summary>
        /// <param name="color">RGB565 has conversion from string, color can be converted from hex format such as passing "#ff0000" string </param>
        public static string TouchDrawingColor(RGB565 color)
        {
            return $"thc={(ushort)color}";
        }

        /// <summary>
        /// Sets internal No-serial-then-sleep timer to specified value in seconds
        ///  min 3, max 65535, default 0 (max: 18 hours 12 minutes 15 seconds)
        /// Nextion will auto-enter sleep mode if and when this timer expires.
        /// Note: Nextion device needs to exit sleep to issue ussp= 0 to disable sleep on no serial, otherwise once ussp is set, it will persist until reboot or reset.
        /// </summary>
        /// <param name="duration">min 3, max 65535 seconds</param>
        public static string SleepOnNoSerial(TimeSpan duration)
        {
            if (duration.TotalSeconds < 3 || duration.TotalSeconds > 65535)
            {
                throw new ArgumentOutOfRangeException("min 3, max 65535");
            }

            return $"ussp={(ushort)duration.TotalSeconds}";
        }

        /// <summary>
        /// Sets internal No-touch-then-sleep timer to specified value in seconds
        /// min 3, max 65535, default 0 (max: 18 hours 12 minutes 15 seconds)
        /// Nextion will auto-enter sleep mode if and when this timer expires.
        /// Note: Nextion device needs to exit sleep to issue thsp= 0 to disable sleep on no touch, otherwise once thsp is set, it will persist until reboot or reset.
        /// </summary>
        /// <param name="duration">min 3, max 65535 seconds</param>
        public static string SleepOnNoTouch(TimeSpan duration)
        {
            if (duration.TotalSeconds < 3 || duration.TotalSeconds > 65535)
            {
                throw new ArgumentOutOfRangeException("min 3, max 65535");
            }

            return $"thsp={(ushort)duration.TotalSeconds}";
        }

        /// <summary>
        /// When value is 1 and Nextion is in sleep mode, the first touch will only trigger the auto wake mode and not trigger a Touch Event.
        /// thup has no influence on sendxy, sendxy will operate independently.
        /// </summary>
        public static string AutoWakeOnTouch(bool enable = true)
        {
            return $"thup={(enable ? "1" : "0")}";
        }

        /// <summary>
        /// Sets if Nextion should send 0x67 and 0x68 Return Data min 0, max 1, default 0 – Less accurate closer to edges, and more accurate closer to center. Note: expecting exact pixel (0,0) or(799,479) is simply not achievable.
        /// </summary>
        public static string SendTouchCoordinates(bool enable = false)
        {
            return $"sendxy={(enable ? "1" : "0")}";
        }

        /// <summary>
        /// reates a halt in Nextion code execution for specified time in ms min 0, max 65535 As delay is interpreted, a total halt is avoided.Incoming serial data is received and stored in buffer but not be processed until delay ends.If delay of more than 65.535 seconds is required, use of multiple delay statements required. delay= -1 is max. 65.535 seconds.
        /// </summary>
        public static string Delay(TimeSpan duration)
        {
            if (duration.TotalMilliseconds > 65535)
            {
                throw new ArgumentOutOfRangeException("min 0, max 65535");
            }
            return $"delay={(ushort)duration.TotalMilliseconds}";
        }

        /// <summary>
        /// reates a halt in Nextion code execution for specified time in ms min 0, max 65535 As delay is interpreted, a total halt is avoided.Incoming serial data is received and stored in buffer but not be processed until delay ends.If delay of more than 65.535 seconds is required, use of multiple delay statements required. delay= -1 is max. 65.535 seconds.
        /// </summary>
        public static string Delay(ushort durationMs)
        {
            return $"delay={durationMs}";
        }

        /// <summary>
        /// Sets Nextion mode between sleep and awake.
        /// min 0, max 1, or default 0
        /// When exiting sleep mode, the Nextion device will auto refresh the page
        /// (as determined by the value in the wup variable) and reset the backlight brightness(as determined by the value in the dim variable). A get/print/printh/wup/sleep instruction can be executed during sleep mode.Extended IO binding interrupts do not occur in sleep.
        /// </summary>
        public static string Sleep(bool enable = false)
        {
            return $"sleep={(enable ? "1" : "0")}";
        }

        public enum ResponseLevel
        {
            /// <summary>
            ///  no pass/fail will be returned
            /// </summary>
            Off,
            /// <summary>
            /// only when last serial command successful.
            /// </summary>
            OnSuccess,
            /// <summary>
            /// only when last serial command failed
            /// </summary>
            OnFailure,
            /// <summary>
            /// returns 0x00 to 0x23 result of serial command.
            /// </summary>
            Always,
        }

        /// <summary>
        /// Sets the level of Return Data on commands processed over Serial.
        /// min 0, max 3, default 2
        /// </summary>
        /// <param name="enable"></param>
        public static string SetResponseLevel(ResponseLevel level = ResponseLevel.OnFailure)
        {
            return $"bkcmd={(int)level}";
        }

        /// <summary>
        /// System Variables are global in nature with no need to define or create.
        /// They can be read or written from any page. 32-bit signed integers.
        /// min value of -2147483648, max value of 2147483647
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string SetSystemVariable(byte index, int value)
        {
            if (index > 2)
            {
                throw new ArgumentOutOfRangeException("0 - 2 only");
            }
            return $"sys{index}={value}";
        }

        /// <summary>
        /// Sets which page Nextion loads when exiting sleep mode
        /// min is 0, max is # of last page in HMI, or default 255
        /// When wup = 255(not set to any existing page)
        /// – Nextion wakes up to current page, refreshing components only
        /// wup can be set even when Nextion is in sleep mode
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <returns></returns>
        public static string SetWakePage(byte pageIndex)
        {
            return $"wup={pageIndex}";
        }

        /// <summary>
        /// Sets if serial data wakes Nextion from sleep mode automatically.
        /// min is 0, max is 1, default 0
        /// When disable, send sleep=0 to wake Nextion
        /// When enable, any serial received wakes Nextion
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <returns></returns>
        public static string WakeOnSerialData(bool enable = false)
        {
            return $"usup={(enable ? "1" : "0")}";
        }

        /// <summary>
        /// RGB565 Color Class for Nextion
        /// </summary>
        public class RGB565
        {
            public byte R;
            public byte G;
            public byte B;

            public static implicit operator ushort(RGB565 color)
            {
                var rScale = color.R / 255d;
                var gScale = color.G / 255d;
                var bScale = color.B / 255d;

                var r5 = (int)Math.Round(rScale * 31, 0);
                var g6 = (int)Math.Round(gScale * 63, 0);
                var b5 = (int)Math.Round(bScale * 31, 0);

                int rgb = (r5 << 11) | (g6 << 5) | b5;

                return (ushort)rgb;
            }

            public static implicit operator RGB565(string hex)
            {
                var start = 0;
                if (hex.StartsWith("#"))
                {
                    start = 1;
                }
                return new RGB565()
                {
                    R = Convert.ToByte(hex[start..(2 + start)], 16),
                    G = Convert.ToByte(hex[(start + 2)..(4 + start)], 16),
                    B = Convert.ToByte(hex[(start + 4)..], 16)
                };
            }
        }
    }
}
