using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Dashboard;

/// <summary>Builds the rows the list asks for, which are only those near the screen.</summary>
internal sealed partial class TableRowFactory(Func<TableItem, UIElement> build) : IElementFactory
{
    public UIElement GetElement(ElementFactoryGetArgs args) =>
        args.Data is TableItem item ? build(item) : new Microsoft.UI.Xaml.Controls.Grid();

    /// <summary>Rows are cheap and differ in shape (group or not), so none are reused.</summary>
    public void RecycleElement(ElementFactoryRecycleArgs args)
    {
    }
}
