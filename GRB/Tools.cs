using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

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
        private static readonly string xmlHead = "<?xml";
        public static bool IsXML(string filename) {
            byte[] buffer = new byte[xmlHead.Length];
			try {
				using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read)) {
					fs.Read(buffer, 0, buffer.Length);
					fs.Close();
                    bool ok = true;
                    for (int i = 0; i < xmlHead.Length; i++) {
                        ok &= (xmlHead[i] == buffer[i]);
                    }
                    return ok;
				}
			} catch (Exception) {}

            return false;
		}

        static readonly Random random = new Random();

        public static string RandomString(int length) {
            var buff = new byte[length];
            random.NextBytes(buff);
            return string.Join("", buff.Select(b => b.ToString("X2")));
		}

        public static string FindAttrValue(XDocument doc, string name, bool caseInsensitive=true) {
            var z = doc.Descendants().First().Descendants();
            foreach (var d in z) {
                if (d.Name.LocalName == "attribute") {
                    var attr = d.FirstAttribute;
                    bool datasetName = false;
                    while (attr != null) {
                        if (attr.Name == "name") {
                            if ( 
                                (attr.Value != name && !caseInsensitive) || 
                                (attr.Value.ToLower() != name.ToLower() && caseInsensitive)
                               ) {
                                break;
                            }
                            datasetName = true;
                        }
                        if (attr.Name == "value" && datasetName) {
                            return attr.Value;
                        }
                        attr = attr.NextAttribute;
                    }
                }
            }

            return null;
        }
	}
}

