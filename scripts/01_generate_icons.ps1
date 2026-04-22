$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$assetsDir = Join-Path $root "src\ProjectArrange.App\Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

$png256 = Join-Path $assetsDir "App-256.png"
$icoPath = Join-Path $assetsDir "App.ico"

Add-Type -AssemblyName System.Drawing | Out-Null

$csharp = @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

public static class IconGen
{
    public static byte[] MakePng(int size)
    {
        using (var bmp = new Bitmap(size, size))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.FromArgb(255, 14, 20, 33));

            using (var brush = new LinearGradientBrush(new Rectangle(0, 0, size, size),
                Color.FromArgb(255, 37, 99, 235),
                Color.FromArgb(255, 16, 185, 129),
                45f))
            using (var path = RoundedRect(new Rectangle((int)(size * 0.12), (int)(size * 0.12), (int)(size * 0.76), (int)(size * 0.76)), (int)(size * 0.18)))
            {
                g.FillPath(brush, path);
            }

            int fontSize = Math.Max(8, (int)(size * 0.32));
            using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                string text = "PA";
                SizeF sz = g.MeasureString(text, font);
                float x = (size - sz.Width) / 2f;
                float y = (size - sz.Height) / 2f;

                using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    float dx = Math.Max(1f, size / 128f);
                    g.DrawString(text, font, shadow, x + dx, y + dx);
                }

                using (var fg = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
                {
                    g.DrawString(text, font, fg, x, y);
                }
            }

            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    public static void WritePng(string path, int size)
    {
        File.WriteAllBytes(path, MakePng(size));
    }

    public static void WriteIco(string path, int[] sizes)
    {
        var images = sizes.Distinct().OrderBy(s => s).Select(s => new { Size = s, Png = MakePng(s) }).ToArray();

        using (var fs = File.Create(path))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)images.Length);

            int offset = 6 + (16 * images.Length);
            foreach (var img in images)
            {
                bw.Write((byte)(img.Size >= 256 ? 0 : img.Size));
                bw.Write((byte)(img.Size >= 256 ? 0 : img.Size));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(img.Png.Length);
                bw.Write(offset);
                offset += img.Png.Length;
            }

            foreach (var img in images)
            {
                bw.Write(img.Png);
            }
        }
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        GraphicsPath path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
"@

try {
    Add-Type -TypeDefinition $csharp -ReferencedAssemblies @("System.Drawing.dll","System.Drawing.Common.dll") | Out-Null
} catch {
    Add-Type -TypeDefinition $csharp -ReferencedAssemblies @("System.Drawing.dll") | Out-Null
}

[IconGen]::WritePng($png256, 256)
[IconGen]::WriteIco($icoPath, @(16, 32, 48, 64, 128, 256))

Write-Host "Generated:"
Write-Host " - $png256"
Write-Host " - $icoPath"
