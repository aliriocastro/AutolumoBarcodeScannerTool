using AutolumoBarcodeScannerTool.Core;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests;

public class SpaceSuppressorTests
{
    [Fact]
    public void Pass_When_VkIsNotSpace()
    {
        SpaceSuppressor.ShouldSuppress(vk: 0x41 /* 'A' */, dwExtraInfo: IntPtr.Zero)
            .ShouldBeFalse();
    }

    [Fact]
    public void Suppress_When_VkIsSpace_AndNoSentinel()
    {
        SpaceSuppressor.ShouldSuppress(vk: 0x20 /* SPACE */, dwExtraInfo: IntPtr.Zero)
            .ShouldBeTrue();
    }

    [Fact]
    public void Pass_When_VkIsSpace_AndSentinelMatches()
    {
        // Our own SendInput re-injection — must not be re-suppressed.
        SpaceSuppressor.ShouldSuppress(vk: 0x20, dwExtraInfo: InjectionSentinel.Value)
            .ShouldBeFalse();
    }

    [Fact]
    public void Suppress_When_VkIsSpace_AndForeignExtraInfo()
    {
        // Some other software's SendInput call would put its own dwExtraInfo.
        // We still suppress (we cannot tell other apps' SendInput from human input).
        SpaceSuppressor.ShouldSuppress(vk: 0x20, dwExtraInfo: new IntPtr(0xDEADBEEF))
            .ShouldBeTrue();
    }
}
