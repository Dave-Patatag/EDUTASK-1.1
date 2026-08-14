using EDUTASK_1._1.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace EDUTASK_1._1.Services;

public static class ProofImageService
{
    public const int MaximumBytes = 20 * 1024 * 1024;
    private const int MaximumDimension = 2048;

    public static async Task<PreparedProofImage> PrepareAsync(FileResult file, CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".pdf"))
            throw new InvalidOperationException("Select one JPEG, PNG, or PDF file.");

        await using Stream input = await file.OpenReadAsync();
        if (extension == ".pdf")
        {
            await using var pdfOutput = new MemoryStream();
            await input.CopyToAsync(pdfOutput, cancellationToken);
            byte[] pdfData = pdfOutput.ToArray();
            if (pdfData.Length == 0 || pdfData.Length > MaximumBytes)
                throw new InvalidOperationException("PDF files must be no larger than 20 MB.");
            if (pdfData.Length < 5 || pdfData[0] != (byte)'%' || pdfData[1] != (byte)'P' ||
                pdfData[2] != (byte)'D' || pdfData[3] != (byte)'F' || pdfData[4] != (byte)'-')
                throw new InvalidOperationException("The selected file is not a valid PDF.");

            return new PreparedProofImage
            {
                Data = pdfData,
                FileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.pdf",
                ContentType = "application/pdf"
            };
        }

        using SharpImage image = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(input, cancellationToken);
        if (image.Width > MaximumDimension || image.Height > MaximumDimension)
            image.Mutate(context => context.Resize(new ResizeOptions { Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max, Size = new SixLabors.ImageSharp.Size(MaximumDimension, MaximumDimension) }));

        string baseName = Path.GetFileNameWithoutExtension(file.FileName);
        byte[] encoded;
        string outputExtension;
        string contentType;

        if (extension == ".png")
        {
            encoded = await EncodePngAsync(image, cancellationToken);
            outputExtension = ".png";
            contentType = "image/png";
            if (encoded.Length > MaximumBytes)
            {
                encoded = await EncodeJpegToLimitAsync(image, cancellationToken);
                outputExtension = ".jpg";
                contentType = "image/jpeg";
            }
        }
        else
        {
            encoded = await EncodeJpegToLimitAsync(image, cancellationToken);
            outputExtension = ".jpg";
            contentType = "image/jpeg";
        }

        if (encoded.Length == 0 || encoded.Length > MaximumBytes)
            throw new InvalidOperationException("This image could not be compressed below 20 MB. Select a smaller image.");

        return new PreparedProofImage { Data = encoded, FileName = $"{baseName}{outputExtension}", ContentType = contentType };
    }

    private static async Task<byte[]> EncodePngAsync(SharpImage image, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        await image.SaveAsync(output, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression }, cancellationToken);
        return output.ToArray();
    }

    private static async Task<byte[]> EncodeJpegToLimitAsync(SharpImage source, CancellationToken cancellationToken)
    {
        using SharpImage working = source.Clone();
        for (int dimension = Math.Max(working.Width, working.Height); dimension >= 640; dimension = (int)(dimension * .82))
        {
            if (Math.Max(working.Width, working.Height) > dimension)
                working.Mutate(context => context.Resize(new ResizeOptions { Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max, Size = new SixLabors.ImageSharp.Size(dimension, dimension) }));

            for (int quality = 88; quality >= 45; quality -= 8)
            {
                await using var output = new MemoryStream();
                await working.SaveAsync(output, new JpegEncoder { Quality = quality }, cancellationToken);
                if (output.Length <= MaximumBytes)
                    return output.ToArray();
            }
        }
        return [];
    }
}