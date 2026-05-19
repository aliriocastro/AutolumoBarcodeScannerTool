using AutolumoBarcodeScannerTool.Core;
using Shouldly;
using Xunit;

namespace AutolumoBarcodeScannerTool.Tests;

public class SpaceArbiterTests
{
    [Fact]
    public async Task RestoresSpace_When_NoOtherKeyArrives()
    {
        var restored = 0;
        var arbiter = new SpaceArbiter(() => Interlocked.Increment(ref restored), delayMs: 30);

        arbiter.OnSpaceSuppressed();
        await Task.Delay(120);

        restored.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotRestore_When_NonSpaceArrivesWithinDelay()
    {
        var restored = 0;
        var arbiter = new SpaceArbiter(() => Interlocked.Increment(ref restored), delayMs: 100);

        arbiter.OnSpaceSuppressed();
        await Task.Delay(20);
        arbiter.OnNonSpaceKey();
        await Task.Delay(200);

        restored.ShouldBe(0);
    }

    [Fact]
    public async Task DoesNotRestore_When_ManyNonSpaceArriveRapidly()
    {
        // Simula scanner: SPACE + '8' + '2' + '8' + '1' + '0' + '8'.
        var restored = 0;
        var arbiter = new SpaceArbiter(() => Interlocked.Increment(ref restored), delayMs: 50);

        arbiter.OnSpaceSuppressed();
        for (var i = 0; i < 6; i++)
        {
            await Task.Delay(5);
            arbiter.OnNonSpaceKey();
        }
        await Task.Delay(200);

        restored.ShouldBe(0);
    }

    [Fact]
    public async Task NonSpace_Without_PendingSpace_DoesNothing()
    {
        var restored = 0;
        var arbiter = new SpaceArbiter(() => Interlocked.Increment(ref restored), delayMs: 30);

        arbiter.OnNonSpaceKey();
        await Task.Delay(100);

        restored.ShouldBe(0);
    }

    [Fact]
    public async Task SecondSpace_Within_Delay_Replaces_First()
    {
        // Dos SPACEs humanos consecutivos rápidos: sólo el último timer dispara.
        // Trade-off documentado en SpaceArbiter — datos normales casi nunca
        // contienen dos SPACEs seguidos.
        var restored = 0;
        var arbiter = new SpaceArbiter(() => Interlocked.Increment(ref restored), delayMs: 50);

        arbiter.OnSpaceSuppressed();
        await Task.Delay(10);
        arbiter.OnSpaceSuppressed();
        await Task.Delay(150);

        restored.ShouldBe(1);
    }
}
