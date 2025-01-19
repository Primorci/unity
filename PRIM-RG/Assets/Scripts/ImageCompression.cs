using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ImageCompression
{
    private static int bitCount = 0;
    public static List<byte> buffer = new List<byte>();

    public static byte[] compress(byte[][] channel, int height, int width)
    {
        int[] E = Predict(channel, height, width);

        int size = width * height;

        int[] N = new int[size];
        N[0] = E[0];
        for (int i = 1; i < size; i++)
        {
            if (E[i] >= 0)
                N[i] = 2 * E[i];
            else
                N[i] = 2 * Math.Abs(E[i]) - 1;
        }

        int[] C = new int[size];
        C[0] = N[0];
        for (int i = 1; i < size; i++)
        {
            C[i] = C[i - 1] + N[i];
        }

        setHeader((short)height, (byte)C[0], C[size - 1], size);

        IC(C, 0, size - 1);

        bitCount = 0;

        return buffer.ToArray();
    }

    private static int[] Predict(byte[][] channel, int h, int w)
    {
        int[] e = new int[w * h];
        for (int x = 0; x < h; x++)
        {
            for (int y = 0; y < w; y++)
            {
                int index = y * h + x;
                if (y == 0 && x == 0)
                    e[index] = channel[0][0];
                else if (y == 0)
                    e[index] = channel[x - 1][0] - channel[x][0];
                else if (x == 0)
                    e[index] = channel[0][y - 1] - channel[0][y];
                else
                {
                    if (channel[x - 1][y - 1] >= Math.Max((int)channel[x - 1][y], (int)channel[x][y - 1]))
                        e[index] = Math.Min((int)channel[x - 1][y], (int)channel[x][y - 1]) - channel[x][y];
                    else if (channel[x - 1][y - 1] <= Math.Min((int)channel[x - 1][y], (int)channel[x][y - 1]))
                        e[index] = Math.Max((int)channel[x - 1][y], (int)channel[x][y - 1]) - channel[x][y];
                    else
                        e[index] = (channel[x - 1][y] + channel[x][y - 1] - channel[x - 1][y - 1]) - channel[x][y];
                }
            }
        }

        return e;
    }

    private static void setHeader(short height, byte firstElement, int lastElement, int totalElements)
    {
        WriteBits(height, 12);
        WriteBits(firstElement, 8);
        WriteBits(lastElement, 32);
        WriteBits(totalElements, 24);
    }

    public static void IC(int[] C, int L, int H)
    {
        if ((H - L) > 1)
        {
            if (C[H] != C[L])
            {
                int m = (int)Math.Floor(0.5 * (H + L));
                int g = (int)Math.Ceiling(Math.Log(C[H] - C[L] + 1, 2));

                int sum = C[m] - C[L];

                //B = Encode(B, g, sum, file, countTimes);
                WriteBits(C[(int)m] - C[L], (int)g);

                if (L < m)
                    IC(C, L, m);

                if (m < H)
                    IC(C, m, H);
            }
        }
    }

    private static int b = 0;
    private static void WriteBits(int value, int numberOfBits)
    {
        // Iterate over the number of bits and write them to the byte array
        for (int i = 0; i < numberOfBits; i++)
        {
            int bit = (value >> (numberOfBits - 1 - i)) & 1; // Extract bit

            b = (b << 1) | bit;
            bitCount++;

            if (bitCount == 8)
            {
                //buffer[offset] = (byte)b;
                buffer.Add((byte)b);
                bitCount = 0;
                b = 0;
            }
        }
    }
}
