using System.Windows.Forms;

namespace MiController
{
    internal class NoHighlightRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Enabled)
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }
}