using System;
using System.Linq;

namespace grbdump {
    public class SecondHeader {

        public PayloadType PayloadType { get; private set; }
        public DateTime Timestamp { get; private set; }
        public int GRBVersion { get; private set; }
        public AssembleIdentifier AssembleIdentifier { get; private set; }
        public SystemEnvironment SystemEnvironment { get; private set; }

        public SecondHeader (byte[] secondHeader) {
            var dseb = secondHeader.Take (2).ToArray ();
            var mseb = secondHeader.Skip (2).Take (4).ToArray ();
            var grbS = secondHeader.Skip (6).Take (2).ToArray ();

            if (BitConverter.IsLittleEndian) {
                Array.Reverse (dseb);
                Array.Reverse (mseb);
                //Array.Reverse (grbS);
            }

            var daysSinceEpoch = BitConverter.ToUInt16(dseb, 0);
            var msOfDay = BitConverter.ToUInt32 (mseb, 0);
            Timestamp = new DateTime (2000, 1, 1, 0, 0, 0);
            Timestamp = Timestamp.AddDays (daysSinceEpoch);
            Timestamp = Timestamp.AddMilliseconds (msOfDay);

            var grbSU = BitConverter.ToUInt16 (grbS, 0);

            GRBVersion = grbSU & 0x1F;
            PayloadType = (PayloadType)(((grbS [1] & 3) << 5) + ((grbS [0] & 0xE0) >> 5));
            AssembleIdentifier = (AssembleIdentifier)((grbSU & 0xC00) >> 10);
            SystemEnvironment = (SystemEnvironment)((grbSU & 0x9000) >> 12);
        }
    }
}

