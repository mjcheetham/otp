using System.Collections;
using QRCoder;

namespace Mjcheetham.Otp;

public static class QRCodeExtensions
{
    extension(QRCodeData qrData)
    {
        public (int width, int height) GetDimensions()
        {
            List<BitArray> matrix = qrData.ModuleMatrix;
            int height = matrix.Count;
            int width = matrix.FirstOrDefault()?.Length ?? 0;
            return (width, height);
        }

        public void ForEachRow(Action<(int Y, BitArray Bits)> action)
        {
            List<BitArray> matrix = qrData.ModuleMatrix;
            int height = matrix.Count;

            for (int y = 0; y < height; y++)
            {
                action((y, matrix[y]));
            }
        }

        public void ForEachPixel(Action<(int X, int Y, bool IsSet)> action)
        {
            List<BitArray> matrix = qrData.ModuleMatrix;
            int height = matrix.Count;
            int width = matrix.FirstOrDefault()?.Length ?? 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    action((x, y, matrix[y][x]));
                }
            }
        }
    }
}
