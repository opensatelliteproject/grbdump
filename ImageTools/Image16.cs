using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenSatelliteProject.IMTools {
    public class Image16 {
        public readonly ushort FillValue = 0xFFFF;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public ushort MaxValue { 
            get { 
                if (dirty) {
                    ComputeMinMax ();
                }

                return cachedMax;
            }  
        }
        public ushort MinValue { 
            get {
                if (dirty) {
                    ComputeMinMax ();
                }

                return cachedMin;
            }
        }

        ushort[][] data;
        bool dirty;
        ushort cachedMax = 0;
        ushort cachedMin = 0xFFFF;

        void Initialize(ref ushort[] array) {
            for (int i = 0; i < array.Length; i++) {
                array [i] = FillValue;
            }
        }

        void Initialize(ref ushort[] array, int index) {
            for (int i = index; i < array.Length; i++) {
                array [i] = FillValue;
            }
        }

        public Image16 (int width, int height, ushort FillValue = 0xFFFF) {
            if (width < 1 || height < 1) {
                throw new ArgumentException($"Invalid with invalid Width / Height: {width}, {height}");
            }
            Width = width;
            Height = height;
            data = new ushort[height][];
            for (int i = 0; i < height; i++) {
                data[i] = new ushort[width];
                Initialize (ref data [i]);
            }
            dirty = true;
            this.FillValue = FillValue;
        }

        public void Resize(int newWidth, int newHeight) {
            // Console.WriteLine ($"Resizing to {newWidth}, {newHeight} from {Width}, {Height}");
            if (Height != newHeight) {
                Array.Resize (ref data, newHeight);
                for (int y = Height; y < newHeight; y++) {
                    data [y] = new ushort[newWidth];
                    Initialize (ref data [y]);
                }
            }

            if (Width != newWidth) {
                for (int y = 0; y < Height; y++) {
                    Array.Resize (ref data [y], newWidth);
                    Initialize (ref data [y], Width);
                }
            }

            Width = newWidth;
            Height = newHeight;
            dirty = true;
        }

        public void DrawImage(Image16 src, int posX, int posY, bool resizeIfNeeded = false) {
            DrawImage (src, new Rectangle (0, 0, src.Width, src.Height), posX, posY, resizeIfNeeded);
        }

        public void DrawImage(Image16 src, Rectangle srcRect, int posX, int posY, bool resizeIfNeeded = false) {
            if (resizeIfNeeded) {
                int neededWidth = Math.Max(posX + srcRect.Width, Width);
                int neededHeight = Math.Max(posY + srcRect.Height, Height);

                if (neededWidth > Width || neededHeight > Height) {
                    Resize (neededWidth, neededHeight);
                }
            }

            int targetX = Math.Max(posX, 0);
            int lenX = Math.Min(srcRect.Width, Width - targetX);
            if (lenX > 0) {
                for (int y = srcRect.Y; y < srcRect.Height; y++) {
                    int targetY = y + posY;
                    if (targetY >= 0 && targetY < Height) {
                        Buffer.BlockCopy (src.data [y], srcRect.X * sizeof(ushort), data [targetY], targetX * sizeof(ushort), lenX * sizeof(ushort));
                    }
                }
                dirty = true;
            }
        }

        public void DrawImage(int[] src, int srcWidth, int srcHeight, int posX, int posY, bool resizeIfNeeded = false) {
            DrawImage (src, srcWidth, new Rectangle (0, 0, srcWidth, srcHeight), posX, posY, resizeIfNeeded);
        }

        public void DrawImage(int[] src, int srcWidth, Rectangle srcRect, int posX, int posY, bool resizeIfNeeded = false) {
            if (resizeIfNeeded) {
                int neededWidth = Math.Max(posX + srcRect.Width, Width);
                int neededHeight = Math.Max(posY + srcRect.Height, Height);

                if (neededWidth > Width || neededHeight > Height) {
                    Resize (neededWidth, neededHeight);
                }
            }
            for (int y = srcRect.Y; y < srcRect.Height; y++) {
                int targetY = posY + y;               
                for (int x = srcRect.X; x < srcRect.Width; x++) {
                    int targetX = posX + x;
                    if ((targetX >= 0 && targetX < Width) && (targetY >= 0 && targetY < Height)) {
                        data [targetY] [targetX] = (ushort)Clamp (src [y * srcWidth + x], 0, 65535);
                    }
                }
            }
            dirty = true;
        }

        public Bitmap ToBitmap() {
            var bmp = new Bitmap (Width, Height, PixelFormat.Format8bppIndexed);

            var pal = bmp.Palette;
            for (int i = 0; i < 256; i++) {
                pal.Entries [i] = Color.FromArgb (i, i, i);
            }

            bmp.Palette = pal;

            float scale = (MaxValue - MinValue) / 255f;
            var byteData = new byte[Height][];
            for (int y = 0; y < Height; y++) {
                byteData [y] = new byte[Width];
                for (int x = 0; x < Width; x++) {
                    byteData [y] [x] = (byte) Clamp((int) ((data [y] [x] - MinValue) / scale), 0, 255);
                }
            }

            var bmpData = bmp.LockBits (new Rectangle (0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            for (int y = 0; y < Height; y++) {
                Marshal.Copy (byteData [y], 0, IntPtr.Add (bmpData.Scan0, y * bmpData.Stride), Width); 
            }
            bmp.UnlockBits (bmpData);
            return bmp;
        }

        public void SavePGM(string filename) {
            using (var fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write)) {
                using (var bw = new BinaryWriter(fs)) {
                    string head = $"P5\n# Generated by OpenSatelliteProject\n{Width} {Height}\n65535\n";
                    for (int i = 0; i < head.Length; i++) {
                        bw.Write ((byte)head[i]);
                    }

                    for (int y = 0; y < Height; y++) {
                        for (int x = 0; x < Width; x++) {
                            byte[] b = BitConverter.GetBytes (data [y] [x]);
                            if (BitConverter.IsLittleEndian) {
                                Array.Reverse (b);
                            }
                            bw.Write(b, 0, 2);
                        }
                    }
                }
            }
        }

        static int Clamp( int value, int min, int max ) {
            return (value < min) ? min : (value > max) ? max : value;
        }

        public void ScaleToBits(int numBits) {
            float maxVal = (float) Math.Round(Math.Pow (2, numBits)) - 1;
            RemoveOffset ();
            float scale = cachedMax / maxVal;
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    data [y][x] = (ushort) (data [y][x] / scale);
                }
            }
            dirty = true;
        }

        public void RemoveOffset() {
            ushort min = MinValue;
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    data [y][x] -= min;
                }
            }
            cachedMax -= min;
            cachedMin = 0;
        }

        void ComputeMinMax() {
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    ushort v = data [y] [x];
                    if (v != FillValue) {
                        cachedMax = Math.Max (cachedMax, v);
                        cachedMin = Math.Min (cachedMin, v); 
                    }
                }
            }
            dirty = false;
        }
    }
}
