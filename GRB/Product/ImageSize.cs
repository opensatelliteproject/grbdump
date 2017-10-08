using System;

namespace OpenSatelliteProject.GRB.Product {
    public class ImageSize {
        public int Width { get; set; }
        public int Height { get; set; }
        public ImageSize (int width, int height) {
            Width = width;
            Height = height;
        }


        public static ImageSize New(int width, int height) {
            return new ImageSize(width, height);
        }
    }
}

