using System;
using System.Diagnostics;
using System.IO;
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

        var newFileName = formFile.FileName.Replace(".pdf", "");

        var tempFilePath = Path.GetTempFileName();
        await using (var stream = File.Create(tempFilePath))
        {
            await formFile.CopyToAsync(stream);
        }

        try
        {
            var pdfToPpm = "/opt/homebrew/bin/pdftoppm";
            var programArguments = $"-png \"{tempFilePath}\" \"{newFileName}\"";
            var startInfo = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                FileName = pdfToPpm,
                Arguments = programArguments
            };

            var process = Process.Start(startInfo);

            if (process == null) throw new Exception("Could not start process");

            var readStdOutTask = process.StandardOutput.ReadToEndAsync();
            var readStdErrorTask = process.StandardError.ReadToEndAsync();

            process.StandardOutput.BaseStream.CopyTo(context.Response.Body);

            await process.WaitForExitAsync();
            var readStdError = await readStdErrorTask;
            var trimmedPdfToPpmErrorString = readStdError.Trim();

            if (process.ExitCode == 0)
            {
                var result = await readStdOutTask;
                await context.Response.WriteAsync(result);

                var imageBytes = File.ReadAllBytes($"{newFileName}-1.png");
                await context.Response.Body.WriteAsync(imageBytes);
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
            File.Delete(tempFilePath);
            File.Delete($"{newFileName}-1.png");
        }
    }
    else
    {
        await context.Response.WriteAsync("No file found in the request!");
    }
});

app.Run();