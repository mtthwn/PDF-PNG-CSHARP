using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseRouting();

app.MapPost("/upload", async context =>
{
    var formFile = context.Request.Form.Files.FirstOrDefault();

    if (formFile is { Length: > 0 })
    {
        if (formFile.ContentType != "application/pdf") await context.Response.WriteAsync("Invalid file type");

        using var memoryStream = new MemoryStream();
        await formFile.CopyToAsync(memoryStream);

        var tempFilePath = Path.GetTempFileName();
        var uploadDirectory = $"uploads/{Path.GetFileNameWithoutExtension(formFile.FileName)}";
        await using (var stream = File.Create(tempFilePath))
        {
            await formFile.CopyToAsync(stream);
        }

        try
        {
            Directory.CreateDirectory(uploadDirectory);
            var pdfToPpm = "/opt/homebrew/bin/pdftoppm";
            var programArguments =
                $"-png \"{tempFilePath}\" \"{uploadDirectory}/{Path.GetFileNameWithoutExtension(formFile.FileName)}\"";
            var startInfo = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                FileName = pdfToPpm,
                Arguments = programArguments,
            };

            var process = Process.Start(startInfo);

            if (process == null) throw new Exception("Could not start process");

            var readStdErrorTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            var readStdError = await readStdErrorTask;
            var trimmedPdfToPpmErrorString = readStdError.Trim();

            if (process.ExitCode == 0)
            {
                var outputDirectory = $"uploads/{Path.GetFileNameWithoutExtension(formFile.FileName)}";

                var pngFiles = Directory.GetFiles(outputDirectory, "*.png");
                var result = new MemoryStream();
                using (var archive = new ZipArchive(result, ZipArchiveMode.Create, true))

                {
                    foreach (var pngFile in pngFiles)
                    {
                        var entryName = Path.GetFileName(pngFile);
                        archive.CreateEntryFromFile(pngFile, entryName, CompressionLevel.Fastest);
                    }
                }

                result.Position = 0;
                result.Seek(0, SeekOrigin.Begin);

                context.Response.Headers.Add("Content-Disposition", "attachment; filename=images.zip");
                context.Response.ContentType = "application/octet-stream";
                await result.CopyToAsync(context.Response.Body);
            }
            else
            {
                Console.WriteLine(process.ExitCode);
                Console.WriteLine(trimmedPdfToPpmErrorString);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            Directory.Delete(uploadDirectory, true);
        }
    }
    else
    {
        await context.Response.WriteAsync("No file found in the request!");
    }
});

app.Run();