using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class FileColumnLayoutTests
{
    /// <summary>Ancho máximo que se recorre en los barridos: una pantalla ancha de sobra.</summary>
    private const int MaxWidth = 2400;

    [Fact]
    public void No_column_ever_falls_below_its_minimum()
    {
        for (int available = 1; available <= MaxWidth; available++)
        {
            int[] widths = FileColumnLayout.Distribute(available);

            Assert.True(widths[0] >= FileColumnLayout.MinName, $"Nombre a {available} px: {widths[0]}");
            Assert.True(widths[1] >= FileColumnLayout.MinType, $"Tipo a {available} px: {widths[1]}");
            Assert.True(widths[2] >= FileColumnLayout.MinSize, $"Tamaño a {available} px: {widths[2]}");
        }
    }

    [Fact]
    public void The_three_columns_fill_the_width_exactly()
    {
        for (int available = 1; available <= MaxWidth; available++)
        {
            int[] widths = FileColumnLayout.Distribute(available);

            // Por debajo del mínimo se aceptan los mínimos; por encima, ni hueco ni desbordamiento.
            int expected = available < FileColumnLayout.Minimum ? FileColumnLayout.Minimum : available;

            Assert.Equal(expected, widths[0] + widths[1] + widths[2]);
        }
    }

    [Fact]
    public void A_narrow_panel_still_keeps_the_three_columns()
    {
        int[] widths = FileColumnLayout.Distribute(120);

        Assert.Equal(FileColumnLayout.MinName, widths[0]);
        Assert.Equal(FileColumnLayout.MinType, widths[1]);
        Assert.Equal(FileColumnLayout.MinSize, widths[2]);
    }

    [Fact]
    public void The_name_column_takes_the_extra_space()
    {
        int[] narrow = FileColumnLayout.Distribute(400);
        int[] wide = FileColumnLayout.Distribute(1200);

        Assert.True(wide[0] - narrow[0] > 700);
        Assert.True(wide[1] <= 160);
        Assert.True(wide[2] <= 110);
    }

    [Fact]
    public void The_layout_is_stable_when_it_is_applied_twice()
    {
        for (int available = 1; available <= MaxWidth; available++)
        {
            int[] first = FileColumnLayout.Distribute(available);
            int[] second = FileColumnLayout.Distribute(available);

            Assert.Equal(first, second);
        }
    }
}
