using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ActiproSoftware.Text;
using ActiproSoftware.Text.Implementation;
using ActiproSoftware.Text.Tagging;
using ActiproSoftware.Text.Tagging.Implementation;
using ActiproSoftware.Windows.Controls.SyntaxEditor;
using ActiproSoftware.Windows.Controls.SyntaxEditor.Highlighting;
using ActiproSoftware.Windows.Controls.SyntaxEditor.Highlighting.Implementation;

namespace ProtoPad_Client
{
    public class ErrorIndicatorTag : IndicatorClassificationTagBase
    {
        private static readonly IClassificationType CustomIndicatorClassificationType = new ClassificationType("Custom Indicator");

        static ErrorIndicatorTag() 
        {
			//var foreground = new SolidColorBrush(Color.FromArgb(0xff, 0x00, 0x40, 0x00));
			//var background = new SolidColorBrush(Color.FromArgb(0x40, 0x8a, 0xf3, 0x82));
            var foreground = new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x40, 0x00));
            var background = new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xbb, 0xbb));
			foreground.Freeze();
			background.Freeze();
			AmbientHighlightingStyleRegistry.Instance.Register(
                CustomIndicatorClassificationType, 
				new HighlightingStyle { Background = background, Foreground = foreground });
		}
		
		public override IClassificationType ClassificationType 
        { 
			get {
				return CustomIndicatorClassificationType;
			}
		}

		public override FrameworkElement CreateGlyph(IEditorViewLine viewLine, TagSnapshotRange<IIndicatorTag> tagRange, Rect bounds) 
        {
			var foreground = new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x40, 0x00));
			var background = new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xbb, 0xbb));
			foreground.Freeze();
			background.Freeze();

			var diameter = Math.Max(8.0, Math.Min(13, Math.Round(Math.Min(bounds.Width, bounds.Height) - 2.0)));
			var grid = new Grid {Width = diameter, Height = diameter};
		    var outerBorder = new Ellipse() {
				Fill = background,
				Stroke = foreground,
				StrokeThickness = 1.0,
			};
			grid.Children.Add(outerBorder);
			return grid;
		}
    }
}