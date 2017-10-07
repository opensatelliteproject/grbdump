using System;
using System.IO;
using OpenSatelliteProject.GRB.Headers;
using OpenSatelliteProject;
using System.Collections.Generic;
using OpenSatelliteProject.IMTools;

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
                // UIConsole.Log($"Moving File {filename} to {f}");
                UIConsole.Log($"New Generic File at {f}");
                File.Move(filename, f);
            } catch (IOException e) {
                UIConsole.Error(String.Format("Error moving file {0} to {1}: {2}", filename, f, e));
            }
        }

        static readonly Dictionary<string, ImageAssembler> ImageCache = new Dictionary<string, ImageAssembler> ();
        static readonly Dictionary<string, ImageAssembler> BigImageCache = new Dictionary<string, ImageAssembler> ();
        static readonly Dictionary<int, ulong> APIDStamp = new Dictionary<int, ulong>();

        public static void HandleFile(string filename, GRBImageHeader header) {
            string dir = Path.GetDirectoryName(filename);
            string ofilename = header.filename ?? Path.GetFileName (filename);
            string bPath = Path.Combine ($"{header.apid:X3}");
            string zPath = Path.Combine (bPath, $"{header.epoch:D16}", $"{header.sequence:D8}");
            string f = Path.Combine (FileHandler.FinalFileFolder, zPath);

            try {
                Directory.CreateDirectory (Path.Combine(FileHandler.FinalFileFolder, bPath, $"{header.epoch:D16}"));
            } catch (IOException e) {
                UIConsole.Error ($"Cannot create directory {bPath}: {e}");
            }

            if (!ImageCache.ContainsKey (zPath)) {
                ImageCache.Add (zPath, new ImageAssembler ((int) header.width, (int)header.height));
            }

            ImageCache [zPath].AppendJ2K (filename);

            try {
                File.Delete (filename);
            } catch (IOException e) {
                UIConsole.Error ($"Error erasing file {filename}: {e}");
            }

            if (ImageCache [zPath].Done) {
                UIConsole.Log ($"New image at {f}");
                ImageCache [zPath].SavePGM ($"{f}.pgm");
                ImageCache [zPath].SaveJPG ($"{f}.jpg");
                File.WriteAllText($"{f}.txt", header.ToString());
                ProcessBigImage (bPath, ImageCache [zPath], header);
                ImageCache.Remove (zPath);
            }
        }

        static void ProcessBigImage(string bPath, ImageAssembler segment, GRBImageHeader header) {
            // UIConsole.Debug ($"Processing BigImage for {bPath}");
            if (!BigImageCache.ContainsKey (bPath)) {
                // UIConsole.Debug ($"Starting for {bPath}");
                BigImageCache [bPath] = new ImageAssembler (segment.Width, segment.Height);
                APIDStamp [header.apid] = header.epoch;
            } else if (APIDStamp.ContainsKey(header.apid) && APIDStamp [header.apid] != header.epoch) {
                string outfolder = Path.Combine (FileHandler.FinalFileFolder, bPath, header.epoch.ToString());
                UIConsole.Log ($"New PRODUCT for {header.apid:X3}. Dumping Old one at {outfolder}.pgm");
                BigImageCache [bPath].AsyncSavePGM ($"{outfolder}.pgm");
                BigImageCache [bPath].AsyncSavePNG ($"{outfolder}.png");
                BigImageCache [bPath] = new ImageAssembler (segment.Width, segment.Height);
                APIDStamp [header.apid] = header.epoch;
            }

            BigImageCache [bPath].DrawAt (segment.Image, (int) header.ulX, (int) header.ulY, true);
        }
    }
}

