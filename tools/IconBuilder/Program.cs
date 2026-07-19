using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: IconBuilder <source.png> <output.ico>");
    return 1;
}

var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(args[1]));
if (outputDirectory is not null) Directory.CreateDirectory(outputDirectory);

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
using var source = new Bitmap(args[0]);
var pngImages = new List<byte[]>();

foreach (var size in sizes)
{
    using var resized = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var graphics = Graphics.FromImage(resized))
    {
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, size, size));
    }

    using var stream = new MemoryStream();
    resized.Save(stream, ImageFormat.Png);
    pngImages.Add(stream.ToArray());
}

using var output = File.Create(args[1]);
using var writer = new BinaryWriter(output);
writer.Write((ushort)0);
writer.Write((ushort)1);
writer.Write((ushort)sizes.Length);

var dataOffset = 6 + (16 * sizes.Length);
for (var i = 0; i < sizes.Length; i++)
{
    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write(pngImages[i].Length);
    writer.Write(dataOffset);
    dataOffset += pngImages[i].Length;
}

foreach (var image in pngImages) writer.Write(image);
Console.WriteLine($"Icon created: {args[1]}");
return 0;
