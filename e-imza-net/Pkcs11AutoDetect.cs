using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EimzaSignerService
{
    /// <summary>PKCS#11 DLL’ini otomatik bulur (kullanıcıdan seçim istemez).</summary>
    public static class Pkcs11AutoDetect
    {
        // Türkiye’de yaygın PKCS#11 dosya adları
        private static readonly string[] KnownNames =
        {
            "akisp11.dll",          // TÜBİTAK AKİS
            "eTPKCS11.dll",         // SafeNet/eToken/Thales
            "idprimepkcs11.dll",    // Gemalto/IDPrime
            "gclib.dll",            // Gemalto classic
            "bit4ipki.dll",         // Bit4Id
            "dkck201.dll",          // Datakey
            "opensc-pkcs11.dll"     // OpenSC
        };

        public static string? TryResolve()
        {
            foreach (var dir in CandidateDirs())
            {
                // önce doğrudan isimlerle bak
                foreach (var name in KnownNames)
                {
                    var full = Path.Combine(dir, name);
                    if (File.Exists(full)) return full;
                }

                // sonra alt klasörlerde hızlı tarama (tek seviye derinlik)
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*.dll", new EnumerationOptions
                    {
                        RecurseSubdirectories = false,
                        IgnoreInaccessible = true
                    }))
                    {
                        var fn = Path.GetFileName(file);
                        if (KnownNames.Any(n => n.Equals(fn, StringComparison.OrdinalIgnoreCase)))
                            return file;
                    }

                    foreach (var sub in Directory.EnumerateDirectories(dir, "*", new EnumerationOptions
                    {
                        RecurseSubdirectories = false,
                        IgnoreInaccessible = true
                    }))
                    {
                        foreach (var file in Directory.EnumerateFiles(sub, "*.dll", new EnumerationOptions
                        {
                            RecurseSubdirectories = false,
                            IgnoreInaccessible = true
                        }))
                        {
                            var fn = Path.GetFileName(file);
                            if (KnownNames.Any(n => n.Equals(fn, StringComparison.OrdinalIgnoreCase)))
                                return file;
                        }
                    }
                }
                catch { /* erişim yoksa atla */ }
            }

            return null; // bulunamadı
        }

        private static IEnumerable<string> CandidateDirs()
        {
            var dirs = new List<string>();

            // Mimariye göre öncelik
            if (Environment.Is64BitProcess)
            {
                dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.System));     // System32 (x64)
                dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            }
            else
            {
                dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));  // SysWOW64 (x86)
                dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            }

            // Tipik kurulum klasörleri
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            dirs.Add(Path.Combine(pf, "TUBITAK", "AKIS"));
            dirs.Add(Path.Combine(pfx, "TUBITAK", "AKIS"));
            dirs.Add(Path.Combine(pf, "SafeNet"));
            dirs.Add(Path.Combine(pfx, "SafeNet"));
            dirs.Add(Path.Combine(pf, "Thales"));
            dirs.Add(Path.Combine(pfx, "Thales"));
            dirs.Add(Path.Combine(pf, "Gemalto"));
            dirs.Add(Path.Combine(pfx, "Gemalto"));
            dirs.Add(Path.Combine(pf, "OpenSC Project"));
            dirs.Add(Path.Combine(pfx, "OpenSC Project"));

            // PATH çevre değişkenindeki klasörler
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var p in pathEnv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (Directory.Exists(p)) dirs.Add(p);

            // tekrarları temizle
            return dirs.Where(Directory.Exists)
                       .Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
