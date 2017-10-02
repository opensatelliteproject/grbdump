using System;
using grbdump.Enum;

namespace grbdump {
    public static class EnumHelpers {

        public static PayloadType APID2Type(uint apid) {
            if (apid >= 0x100 && apid <= 0x19f) { // ABI
                uint s = apid / 0x10;
                return (s % 2) > 0 ? PayloadType.ImageData : PayloadType.Generic;
            }

            return (apid >= 0x486 && apid <= 0x48b) ? PayloadType.ImageData : PayloadType.Generic;
        }
        public static PayloadType APID2Type(int apid) {
            if (apid >= 0x100 && apid <= 0x19f) { // ABI
                int s = apid / 0x10;
                return (s % 2) > 0 ? PayloadType.ImageData : PayloadType.Generic;
            }

            return (apid >= 0x486 && apid <= 0x48b) ? PayloadType.ImageData : PayloadType.Generic;
        }

        public static Instrument APID2Instrument(uint apid) {
            if (apid >= 0x100 && apid <= 0x19f) {
                return Instrument.ABI;
            }
            if (apid >= 0x300 && apid <= 0x303) {
                return Instrument.GLM;
            }
            if (apid >= 0x380 && apid <= 0x383) {
                return Instrument.EXIS;
            }
            if (apid >= 0x400 && apid <= 0x431) {
                return Instrument.SEISS;
            }
            if (apid >= 0x480 && apid <= 0x48b) {
                return Instrument.SUVI;
            }
            if (apid == 0x500 || apid == 0x501) {
                return Instrument.MAG;
            }
            if (apid == 0x580) {
                return Instrument.INFO;
            }

            return Instrument.Unknown;
        }
    }
}

