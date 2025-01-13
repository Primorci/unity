using System.IO;
using UnityEngine;
using System.Drawing;
using System;
using NUnit.Framework.Internal;

public class CameraScreenshot : MonoBehaviour
{
    public Camera targetCamera; // Assign the specific camera in the Inspector
    public int imageWidth = 1920;
    public int imageHeight = 1080;
    public string BMPsavePath = "Assets/Screenshots/Raw/";
    public string BMPfileName = "screenshot";
    public string binSavePath = "Assets/Screenshots/Compress/";
    public string binFileName = "compressed";

    private byte[] pixelsRaw;
    private Color32[] pixels;
    public void compressBMP()
    {
        byte[][] R = new byte[imageHeight][];

        for (int i = 0; i < imageHeight; i++)
        {
            R[i] = new byte[imageWidth];
        }

        // Fill the 2D array with data from the 1D array
        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                // Calculate the index in the 1D array (flattened representation)
                int index = y * imageWidth + x;
                R[y][x] = pixels[index].r;
            }
        }

        string binFullPath = binSavePath + binFileName + "R" + Time.realtimeSinceStartup + ".bin";

        byte[] compressed = ImageCompression.compress(R, imageHeight, imageWidth);


        using (BinaryWriter fs = new BinaryWriter(File.Open(binFullPath, FileMode.Create)))
        {
            fs.Flush();
            fs.Write(compressed);
            fs.Close();
        }
    }

    public static void SaveCompressed(string filePath, byte[][] R, byte[][] G, byte[][] B, int width, int height)
    {
        using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
        {
            // Write header
            writer.Write(width);
            writer.Write(height);

            // Flatten and write channels
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    writer.Write(R[y][x]);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    writer.Write(G[y][x]);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    writer.Write(B[y][x]);
        }
    }

    public void SaveTextureAsBMP()
    {
        // Create a RenderTexture
        RenderTexture renderTexture = new RenderTexture(imageWidth, imageHeight, 24);
        targetCamera.targetTexture = renderTexture;

        // Render the camera's view to the RenderTexture
        RenderTexture.active = renderTexture;
        targetCamera.Render();

        // Create a Texture2D to hold the rendered image
        Texture2D screenshot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenshot.Apply();

        // Ensure the texture is readable
        if (!screenshot.isReadable)
        {
            Debug.LogError("Texture must be readable to save as BMP.");
            return;
        }

        // Get pixel data
        pixels = screenshot.GetPixels32();
        pixelsRaw = screenshot.GetRawTextureData();
        int width = screenshot.width;
        int height = screenshot.height;

        // Create BMP file
        byte[] bmpData = CreateBMP(pixels, width, height);
       
        string path = BMPsavePath + BMPfileName + Time.realtimeSinceStartup + ".bmp";

        // Write BMP to file
        File.WriteAllBytes(path, bmpData);
        Debug.Log($"BMP saved to {path}");
    }

    private byte[] CreateBMP(Color32[] pixels, int width, int height)
    {
        int fileSize = 54 + (width * height * 3); // Header (54 bytes) + Pixel Data
        byte[] bmp = new byte[fileSize];

        // File Header
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M'; // Signature
        System.BitConverter.GetBytes(fileSize).CopyTo(bmp, 2); // File size
        bmp[10] = 54; // Data offset

        // DIB Header
        bmp[14] = 40; // Header size
        System.BitConverter.GetBytes(width).CopyTo(bmp, 18); // Width
        System.BitConverter.GetBytes(height).CopyTo(bmp, 22); // Height
        bmp[26] = 1; // Planes
        bmp[28] = 24; // Bits per pixel
        bmp[34] = (byte)(width * height * 3); // Image size (row-aligned)

        // Pixel Data
        int dataOffset = 54;
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = (height - y - 1) * width + x; // BMP stores rows bottom-to-top
                Color32 pixel = pixels[pixelIndex];

                bmp[dataOffset++] = pixel.b; // Blue
                bmp[dataOffset++] = pixel.g; // Green
                bmp[dataOffset++] = pixel.r; // Red
            }
        }

        return bmp;
    }

    // Optional: Test the capture with a key press
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            //CaptureScreenshot();
            SaveTextureAsBMP();
            compressBMP();
        }
    }
}
