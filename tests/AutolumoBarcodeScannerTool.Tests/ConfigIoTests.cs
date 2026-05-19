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
        // do not create file

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
                TargetProcessName: "MiAppContable",
                TargetWindowTitleContains: "Factura",
                AutostartEnabled: false);

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
            cfg.TargetProcessName.ShouldBe(ScannerConfig.Default.TargetProcessName);
            cfg.AutostartEnabled.ShouldBe(ScannerConfig.Default.AutostartEnabled);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_IgnoresUnknownKeys()
    {
        // Compatibility with the v0.1.5 .ini which had a Scanner:Serial:BaudRate etc.
        var path = TempPath();
        File.WriteAllText(path,
            "VendorId=0xAAAA\n" +
            "Scanner:Serial:BaudRate=9600\n" +
            "Scanner:Diagnostics:VerboseLogging=true\n" +
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
            "  ProductId  =  0x8888  \n" +
            "TargetProcessName=AppX\n");
        try
        {
            var cfg = ConfigIo.Load(path);

            cfg.VendorId.ShouldBe("0x9999");
            cfg.ProductId.ShouldBe("0x8888");
            cfg.TargetProcessName.ShouldBe("AppX");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        var path = TempPath();
        try
        {
            ConfigIo.Save(path, new ScannerConfig("0x1111", "0x2222", "AppA", "", true));
            ConfigIo.Save(path, new ScannerConfig("0x3333", "0x4444", "AppB", "filter", false));

            var loaded = ConfigIo.Load(path);

            loaded.VendorId.ShouldBe("0x3333");
            loaded.ProductId.ShouldBe("0x4444");
            loaded.TargetProcessName.ShouldBe("AppB");
            loaded.TargetWindowTitleContains.ShouldBe("filter");
            loaded.AutostartEnabled.ShouldBeFalse();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_MalformedBoolValue_FallsBackToDefault()
    {
        // A hand-edited "yes" / "1" / "on" must NOT silently disable a
        // default-true setting. Falls back to ScannerConfig.Default.AutostartEnabled.
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
