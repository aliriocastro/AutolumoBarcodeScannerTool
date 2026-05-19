using AutolumoBarcodeScannerTool.Core.Configuration;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests.Configuration;

public class IniConfigWriterTests
{
    [Fact]
    public void Set_ExistingKey_ReplacesValueInSamePosition()
    {
        var original = """
            ; comment
            [Section]
            Key=oldValue
            Other=foo
            """;

        var result = IniConfigWriter.Update(original, "Section:Key", "newValue");

        result.ShouldBe("""
            ; comment
            [Section]
            Key=newValue
            Other=foo
            """);
    }

    [Fact]
    public void Set_NewKeyInExistingSection_AppendsToSection()
    {
        var original = """
            [Section]
            Existing=1
            """;

        var result = IniConfigWriter.Update(original, "Section:NewKey", "value");

        result.ShouldBe("""
            [Section]
            Existing=1
            NewKey=value
            """);
    }

    [Fact]
    public void Set_NewSection_AppendsAtEnd()
    {
        var original = """
            [Existing]
            A=1
            """;

        var result = IniConfigWriter.Update(original, "NewSection:Key", "value");

        result.ShouldBe("""
            [Existing]
            A=1

            [NewSection]
            Key=value
            """);
    }

    [Fact]
    public void Set_PreservesCommentsAboveTargetLine()
    {
        var original = """
            [Section]
            ; documenta Key
            Key=old
            """;

        var result = IniConfigWriter.Update(original, "Section:Key", "new");

        result.ShouldBe("""
            [Section]
            ; documenta Key
            Key=new
            """);
    }

    [Fact]
    public void Set_HierarchicalSectionWithColon_Works()
    {
        var original = """
            [Scanner:Serial]
            PortName=COM3
            """;

        var result = IniConfigWriter.Update(original, "Scanner:Serial:PortName", "COM5");

        result.ShouldBe("""
            [Scanner:Serial]
            PortName=COM5
            """);
    }

    [Fact]
    public void Set_EmptyValue_WritesEmptyValue()
    {
        var original = """
            [Section]
            Key=oldValue
            """;

        var result = IniConfigWriter.Update(original, "Section:Key", "");

        result.ShouldBe("""
            [Section]
            Key=
            """);
    }
}
