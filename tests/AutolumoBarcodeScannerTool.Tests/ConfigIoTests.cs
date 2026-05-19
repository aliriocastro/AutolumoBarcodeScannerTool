using AutolumoBarcodeScannerTool.Core;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests;

public class ConfigIoTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(),
        $"autolumo-cfgio-{Guid.NewGuid():N}.ini");

    [Fact]
    public void Load_MissingFile_ReturnsDefault()
    {
        var path = TempPath();

        var cfg = ConfigIo.Load(path);

        cfg.ShouldBe(ScannerConfig.Default);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var path = TempPath();
        try
        {
            var original = new ScannerConfig(
                VendorId: "0x0C2E",
                ProductId: "0x0B61",
                AutostartEnabled: false,
                VerboseLogging: true);

            ConfigIo.Save(path, original);
            var loaded = ConfigIo.Load(path);

            loaded.ShouldBe(original);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_PartialFile_FillsMissingFieldsWithDefault()
    {
        var path = TempPath();
        File.WriteAllText(path,
            "VendorId=0x1234\n" +
            "ProductId=0xABCD\n");
        try
        {
            var cfg = ConfigIo.Load(path);

            cfg.VendorId.ShouldBe("0x1234");
            cfg.ProductId.ShouldBe("0xABCD");
            cfg.AutostartEnabled.ShouldBe(ScannerConfig.Default.AutostartEnabled);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_IgnoresUnknownKeys()
    {
        // Compatibilidad con .ini de v0.2.1 que tenía TargetProcessName,
        // IgnoreWindowFilter, etc. — ahora se ignoran silenciosamente.
        var path = TempPath();
        File.WriteAllText(path,
            "VendorId=0xAAAA\n" +
            "TargetProcessName=oldfield\n" +
            "IgnoreWindowFilter=true\n" +
            "ProductId=0xBBBB\n");
        try
        {
            var cfg = ConfigIo.Load(path);

            cfg.VendorId.ShouldBe("0xAAAA");
            cfg.ProductId.ShouldBe("0xBBBB");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_IgnoresCommentsAndBlankLinesAndSectionHeaders()
    {
        var path = TempPath();
        File.WriteAllText(path,
            "; comment line\n" +
            "# hash comment\n" +
            "[Scanner]\n" +
            "\n" +
            "VendorId=0x9999\n" +
            "  ProductId  =  0x8888  \n");
        try
        {
            var cfg = ConfigIo.Load(path);

            cfg.VendorId.ShouldBe("0x9999");
            cfg.ProductId.ShouldBe("0x8888");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        var path = TempPath();
        try
        {
            ConfigIo.Save(path, new ScannerConfig("0x1111", "0x2222", true, false));
            ConfigIo.Save(path, new ScannerConfig("0x3333", "0x4444", false, true));

            var loaded = ConfigIo.Load(path);

            loaded.VendorId.ShouldBe("0x3333");
            loaded.ProductId.ShouldBe("0x4444");
            loaded.AutostartEnabled.ShouldBeFalse();
            loaded.VerboseLogging.ShouldBeTrue();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_MalformedBoolValue_FallsBackToDefault()
    {
        var path = TempPath();
        File.WriteAllText(path, "AutostartEnabled=yes\n");
        try
        {
            var cfg = ConfigIo.Load(path);

            cfg.AutostartEnabled.ShouldBe(ScannerConfig.Default.AutostartEnabled);
        }
        finally { File.Delete(path); }
    }
}
