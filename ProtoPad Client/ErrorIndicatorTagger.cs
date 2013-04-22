using ActiproSoftware.Text;
using ActiproSoftware.Text.Tagging;
using ActiproSoftware.Text.Tagging.Implementation;
using ActiproSoftware.Text.Utility;

namespace ProtoPad_Client
{
    public class ErrorIndicatorTagger : IndicatorClassificationTaggerBase<ErrorIndicatorTag> 
    {
        public ErrorIndicatorTagger(ICodeDocument document) : base("CustomIndicator", new[] { 
				new Ordering(TaggerKeys.Token, OrderPlacement.Before)
			}, document, true) {}
    }
}