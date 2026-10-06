using System.Drawing;
using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar.Tests.Rendering;

public sealed class WidgetRendererTests : IDisposable
{
    /// <summary>Anything more opaque than this counts as drawn content, not background.</summary>
    private const int ContentAlphaThreshold = 40;

    /// <summary>Content must keep at least this many pixels (at 100%) from every edge.</summary>
    private const int MinimumInset = 2;

    private static readonly WidgetContent SampleContent =
        new("$228.76 today", "$19.0/h · month $3,449");

    private readonly WidgetRenderer renderer = new();

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void Scales_its_height_with_the_display(double scale)
    {
        using var bitmap = renderer.Render(SampleContent, WidgetPalette.DarkTaskbar, scale, false);

        Assert.Equal((int)Math.Round(40 * scale), bitmap.Height);
    }

    [Theory]
    [InlineData(1e-7)]
    [InlineData(0.0005)]
    [InlineData(double.NaN)]
    public void Draws_a_ring_too_small_to_see_without_failing(double fraction)
    {
        var sliver = SampleContent with
        {
            Ring = new WidgetRing(fraction, WidgetTone.Normal, fraction, WidgetTone.Normal),
        };

        using var bitmap = renderer.Render(sliver, WidgetPalette.DarkTaskbar, 1.25, false);

        Assert.True(bitmap.Width > 0);
    }

    [Fact]
    public void Draws_extra_panels_beside_the_first()
    {
        var two = SampleContent with { MorePanels = [SampleContent] };

        using var one = renderer.Render(SampleContent, WidgetPalette.DarkTaskbar, 1.0, false);
        using var both = renderer.Render(two, WidgetPalette.DarkTaskbar, 1.0, false);

        Assert.True(both.Width > one.Width * 2);
        Assert.Equal(one.Height, both.Height);
    }

    /// <summary>A ring's middle is empty; a full one is a solid disc with a white bar.</summary>
    [Fact]
    public void Draws_a_full_ring_as_a_solid_stop_disc()
    {
        var palette = WidgetPalette.DarkTaskbar;
        var full = SampleContent with { Ring = new WidgetRing(1.0, WidgetTone.Critical) };
        var partial = SampleContent with { Ring = new WidgetRing(0.5, WidgetTone.Critical) };

        using var fullBitmap = renderer.Render(full, palette, 1.0, false);
        using var partialBitmap = renderer.Render(partial, palette, 1.0, false);
        var ringMiddle = new Point(10 + 11, 20);
        var belowBar = new Point(10 + 11, 26);

        Assert.True(partialBitmap.GetPixel(ringMiddle.X, belowBar.Y).A < ContentAlphaThreshold);
        Assert.True(fullBitmap.GetPixel(belowBar.X, belowBar.Y).A > 200);
        Assert.True(fullBitmap.GetPixel(ringMiddle.X, ringMiddle.Y).GetBrightness() > 0.9f);
    }

    [Fact]
    public void Equal_panels_make_equal_content()
    {
        var first = SampleContent with { MorePanels = [SampleContent] };
        var second = SampleContent with { MorePanels = [SampleContent with { }] };

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void Makes_room_for_the_ring_without_touching_an_edge(double scale)
    {
        var withRing = SampleContent with { Ring = new WidgetRing(0.67) };

        using var plain = renderer.Render(SampleContent, WidgetPalette.DarkTaskbar, scale, false);
        using var ringed = renderer.Render(withRing, WidgetPalette.DarkTaskbar, scale, false);

        Assert.True(ringed.Width > plain.Width);
        var inset = (int)Math.Ceiling(MinimumInset * scale);
        for (var row = 0; row < ringed.Height; row++)
        {
            Assert.True(ringed.GetPixel(inset - 1, row).A < ContentAlphaThreshold);
        }
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Never_draws_text_against_an_edge(double scale)
    {
        using var bitmap = renderer.Render(SampleContent, WidgetPalette.DarkTaskbar, scale, false);
        var inset = (int)Math.Ceiling(MinimumInset * scale);

        var content = FindContentBounds(bitmap);

        Assert.True(content.Left >= inset, $"left {content.Left} < {inset}");
        Assert.True(content.Top >= inset, $"top {content.Top} < {inset}");
        Assert.True(bitmap.Width - content.Right >= inset, $"right gap below {inset}");
        Assert.True(bitmap.Height - content.Bottom >= inset, $"bottom gap below {inset}");
    }

    [Theory]
    [InlineData(0.1, true)]
    [InlineData(0.5, true)]
    [InlineData(0.9, true)]
    [InlineData(0.5, false)]
    public void Keeps_moving_text_away_from_the_edges(double progress, bool movesText)
    {
        var next = new WidgetContent("$1,228.76 today", "Full in ~6 min", WidgetTone.Critical);
        var frame = new WidgetFrame(next, SampleContent, progress, progress, 0, movesText);

        using var bitmap = renderer.Render(frame, WidgetPalette.DarkTaskbar, 1.5);
        var content = FindContentBounds(bitmap);
        var inset = (int)Math.Ceiling(MinimumInset * 1.5);

        Assert.True(content.Top >= inset && content.Left >= inset);
        Assert.True(bitmap.Height - content.Bottom >= inset);
        Assert.True(bitmap.Width - content.Right >= inset);
    }

    [Fact]
    public void Grows_wider_for_longer_text_instead_of_clipping()
    {
        var longContent = SampleContent with
        {
            SecondaryText = "$19.0/h · month $3,449 · week $812",
        };

        using var shortBitmap = renderer.Render(SampleContent, WidgetPalette.DarkTaskbar, 1, false);
        using var longBitmap = renderer.Render(longContent, WidgetPalette.DarkTaskbar, 1, false);

        Assert.True(longBitmap.Width > shortBitmap.Width);
    }

    [Fact]
    public void Keeps_the_background_clickable()
    {
        using var bitmap = renderer.Render(SampleContent, WidgetPalette.DarkTaskbar, 1, false);

        Assert.True(bitmap.GetPixel(1, bitmap.Height / 2).A > 0);
    }

    public void Dispose() => renderer.Dispose();

    private static Rectangle FindContentBounds(Bitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = 0, bottom = 0;
        for (var row = 0; row < bitmap.Height; row++)
        {
            for (var column = 0; column < bitmap.Width; column++)
            {
                if (bitmap.GetPixel(column, row).A <= ContentAlphaThreshold)
                {
                    continue;
                }

                left = Math.Min(left, column);
                top = Math.Min(top, row);
                right = Math.Max(right, column + 1);
                bottom = Math.Max(bottom, row + 1);
            }
        }

        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
