using System;
using System.IO;
using OpenSatelliteProject;
using grbdump.GRB;

namespace grbdump {
    public static class GRBFileHandler {

        public static void HandleFile(string filename, GRBGenericHeader header) {
            string dir = Path.GetDirectoryName(filename);
            string ofilename = header.filename ?? Path.GetFileName (filename);
            string f = Path.Combine (FileHandler.FinalFileFolder, $"{header.apid:X3}");
            try {
                Directory.CreateDirectory (f);
            } catch (IOException e) {
                UIConsole.Error ($"Cannot create directory {f}: {e}");
            }
            f = Path.Combine(f, ofilename);

            if (File.Exists(f)) {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssffff");
                string ext = Path.GetExtension(f);
                string append = String.Format("--dup-{0}{1}", timestamp, ext);
                f = f.Replace(String.Format("{0}", ext), append);
            }

            try {
                UIConsole.Log($"Moving File {filename} to {f}");
                File.Move(filename, f);
            } catch (IOException e) {
                UIConsole.Error(String.Format("Error moving file {0} to {1}: {2}", filename, f, e));
            }
        }

        public static void HandleFile(string filename, GRBImageHeader header) {
            string dir = Path.GetDirectoryName(filename);
            string ofilename = header.filename ?? Path.GetFileName (filename);
            string f = Path.Combine (FileHandler.FinalFileFolder, $"{header.apid:X3}");
            f = Path.Combine (f, $"{header.epoch:D16}");
            f = Path.Combine (f, $"{header.sequence:D8}");
            try {
                Directory.CreateDirectory (f);
            } catch (IOException e) {
                UIConsole.Error ($"Cannot create directory {f}: {e}");
            }

            int c = 0;
            string fn = Path.Combine(f, $"{c:D4}.j2k");
            while (File.Exists (fn)) {
                c++;
                fn = Path.Combine(f, $"{c:D4}.j2k");
            }

            f = fn;

            try {
                UIConsole.Log($"Moving File {filename} to {f}");
                File.Move(filename, f);
                File.WriteAllText(f.Replace(".img", ".txt").Replace(".j2k", ".txt"), header.ToString());
            } catch (IOException e) {
                UIConsole.Error(String.Format("Error moving file {0} to {1}: {2}", filename, f, e));
            }
        }
    }
}

