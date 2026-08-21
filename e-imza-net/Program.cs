using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PdfRect = iText.Kernel.Geom.Rectangle;

namespace EimzaSignerService;

public class Program
{
    private const long MaxPdfSize = 50L * 1024 * 1024;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 52L * 1024 * 1024);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("signing", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        var app = builder.Build();
        app.UseRateLimiter();

        var signerApiKey = builder.Configuration["SIGNER_API_KEY"];
        if (string.IsNullOrWhiteSpace(signerApiKey))
            throw new InvalidOperationException("SIGNER_API_KEY yapılandırılmalıdır.");

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals("/api/sign", StringComparison.OrdinalIgnoreCase))
            {
                var suppliedKey = context.Request.Headers["X-Signer-Key"].ToString();
                var expectedBytes = Encoding.UTF8.GetBytes(signerApiKey);
                var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
                if (expectedBytes.Length != suppliedBytes.Length ||
                    !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await next();
        });

        app.MapPost("/api/sign", async (
            [FromForm] IFormFile file,
            [FromForm] string pin,
            [FromForm] SignaturePosition signaturePosition,
            [FromServices] ILogger<Program> logger) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { Message = "İmzalanacak dosya gönderilmedi." });
            if (file.Length > MaxPdfSize)
                return Results.Problem("PDF belgesi en fazla 50 MB olabilir.", statusCode: StatusCodes.Status413PayloadTooLarge);
            if (string.IsNullOrWhiteSpace(pin) || pin.Length is < 4 or > 16 || !pin.All(char.IsDigit))
                return Results.BadRequest(new { Message = "PIN 4-16 rakamdan oluşmalıdır." });

            var originalFileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 200 ||
                !originalFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { Message = "Geçerli bir PDF dosyası gönderin." });

            var tempDir = Path.Combine(Path.GetTempPath(), $"eimza_{Guid.NewGuid():N}");
            var inputPath = Path.Combine(tempDir, "input.pdf");
            var outputPath = Path.Combine(tempDir, "signed.pdf");
            Directory.CreateDirectory(tempDir);

            try
            {
                await using (var stream = new FileStream(inputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await file.CopyToAsync(stream);

                await using (var stream = File.OpenRead(inputPath))
                {
                    var prefix = new byte[Math.Min(1024, (int)stream.Length)];
                    _ = await stream.ReadAsync(prefix);
                    if (!Encoding.Latin1.GetString(prefix).Contains("%PDF-", StringComparison.Ordinal))
                        return Results.BadRequest(new { Message = "Dosya içeriği geçerli bir PDF değil." });
                }

                using (var pdfReader = new PdfReader(inputPath))
                using (var pdfDoc = new PdfDocument(pdfReader))
                {
                    var pageNumber = pdfDoc.GetNumberOfPages();
                    if (pageNumber < 1)
                        return Results.BadRequest(new { Message = "PDF belgesinde imzalanabilir sayfa bulunamadı." });

                    var signatureRect = CalculateSignatureRectangle(pdfDoc.GetPage(pageNumber).GetPageSize(), signaturePosition);
                    var libraryPath = Pkcs11AutoDetect.TryResolve();
                    if (string.IsNullOrEmpty(libraryPath))
                        return Results.Problem("E-imza altyapısı yapılandırılmamış.");

                    using var pkcsService = new Pkcs11Service(libraryPath);
                    if (!pkcsService.IsReady)
                    {
                        logger.LogError("PKCS#11 kitaplığı yüklenemedi: {LoadError}", pkcsService.LastLoadError);
                        return Results.Problem("E-imza kitaplığı yüklenemedi.");
                    }

                    var certificate = pkcsService.GetCertificates().FirstOrDefault();
                    if (certificate == null)
                        return Results.Problem("E-imza tokenında geçerli sertifika bulunamadı.");

                    new EimzaPdfSigner().SignPdf(
                        inputPath, outputPath, certificate, pin, pkcsService, signatureRect, pageNumber);
                }

                var signedFileBytes = await File.ReadAllBytesAsync(outputPath);
                logger.LogInformation("Belge başarıyla imzalandı.");
                return Results.File(signedFileBytes, "application/pdf", "signed_" + originalFileName);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "İmzalama sırasında kritik hata oluştu.");
                return Results.Problem("İmzalama işlemi tamamlanamadı. Ayrıntılar güvenli sunucu günlüğüne kaydedildi.");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Geçici imzalama klasörü silinemedi.");
                }
            }
        })
        .DisableAntiforgery()
        .RequireRateLimiting("signing");

        app.Run();
    }

    private static PdfRect CalculateSignatureRectangle(PdfRect pageSize, SignaturePosition position)
    {
        const float boxWidthMm = 80f;
        const float boxHeightMm = 20f;
        const float marginMm = 15f;
        var width = (float)(boxWidthMm * 72.0 / 25.4);
        var height = (float)(boxHeightMm * 72.0 / 25.4);
        var margin = (float)(marginMm * 72.0 / 25.4);

        return position switch
        {
            SignaturePosition.BottomRight => new PdfRect(pageSize.GetRight() - width - margin, margin, width, height),
            SignaturePosition.BottomLeft => new PdfRect(margin, margin, width, height),
            SignaturePosition.TopRight => new PdfRect(pageSize.GetRight() - width - margin, pageSize.GetTop() - height - margin, width, height),
            SignaturePosition.TopLeft => new PdfRect(margin, pageSize.GetTop() - height - margin, width, height),
            _ => new PdfRect(pageSize.GetRight() - width - margin, margin, width, height)
        };
    }
}
