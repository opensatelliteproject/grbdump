using System;

namespace OpenSatelliteProject.GRB {
    public static class Tools {

        public static uint CalcCRC(byte[] data) {
            Crc32 crc32 = new Crc32();
            return crc32.Get (data);
        }
        public static uint CRC32(this byte[] data) {
            return CalcCRC(data);
        }

        public static void Print(this byte[] data) {
            const int lineSize = 24;
            var sb = "";
            var bytesLine = "";
            for (int i = 0; i < data.Length; i++) {
                if (i % lineSize == 0) {
                    if (bytesLine.Length > 0) {
                        sb += "| " + bytesLine + "\n| ";
                        bytesLine = "";
                    } else {
                        sb += "\n| ";
                    }
                }
                sb += data [i].ToString ("X2") + " ";
                bytesLine += !char.IsControl ((char)data [i]) ? Convert.ToChar(data [i]).ToString() : ".";
                if (i % 4 == 3) {
                    sb += " ";  
                }
            }
            Console.WriteLine (sb);
        }
    }
}

