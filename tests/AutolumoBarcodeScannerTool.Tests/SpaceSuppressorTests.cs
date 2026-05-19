using AutolumoBarcodeScannerTool.Core;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests;

public class SpaceSuppressorTests
{
    private const uint LLKHF_INJECTED = 0x10;

    [Fact]
    public void Pass_When_VkIsNotSpace()
    {
        SpaceSuppressor.ShouldSuppress(vk: 0x41 /* 'A' */, flags: 0, dwExtraInfo: IntPtr.Zero)
            .ShouldBeFalse();
    }

    [Fact]
    public void Suppress_When_VkIsSpace_AndPhysical()
    {
        SpaceSuppressor.ShouldSuppress(vk: 0x20, flags: 0, dwExtraInfo: IntPtr.Zero)
            .ShouldBeTrue();
    }

    [Fact]
    public void Pass_When_VkIsSpace_AndInjectedFlagSet()
    {
        // Cualquier SendInput (incluido el nuestro) tiene LLKHF_INJECTED — pasa.
        SpaceSuppressor.ShouldSuppress(vk: 0x20, flags: LLKHF_INJECTED, dwExtraInfo: IntPtr.Zero)
            .ShouldBeFalse();
    }

    [Fact]
    public void Pass_When_VkIsSpace_AndSentinelMatches()
    {
        // Si por alguna razón Windows filtrara el flag injected en una ruta
        // futura, el sentinel sigue siendo señal suficiente para pasar.
        SpaceSuppressor.ShouldSuppress(vk: 0x20, flags: 0, dwExtraInfo: InjectionSentinel.Value)
            .ShouldBeFalse();
    }

    [Fact]
    public void Suppress_When_VkIsSpace_AndForeignExtraInfoNoFlag()
    {
        // dwExtraInfo arbitrario sin el flag injected (hipotético): suprimir.
        SpaceSuppressor.ShouldSuppress(vk: 0x20, flags: 0, dwExtraInfo: new IntPtr(0xDEADBEEF))
            .ShouldBeTrue();
    }
}
