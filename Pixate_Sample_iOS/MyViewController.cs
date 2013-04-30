using System;
using System.IO;
using MonoTouch.UIKit;
using System.Drawing;
using PixateLib;
using ProtoPadServerLibrary_iOS;

namespace Pixate_Sample_iOS
{
    public class MyViewController : UIViewController
    {
        UIButton _button;
        int _numClicks;
        private const float ButtonWidth = 200;
        private const float ButtonHeight = 50;
        private ProtoPadServer _protoPadServer;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            View.Frame = UIScreen.MainScreen.Bounds;
            View.BackgroundColor = UIColor.White;
            View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            _button = UIButton.FromType(UIButtonType.RoundedRect);

            _button.Frame = new RectangleF(
                View.Frame.Width / 2 - ButtonWidth / 2,
                View.Frame.Height / 2 - ButtonHeight / 2,
                ButtonWidth,
                ButtonHeight);

            _button.SetTitle("Click me", UIControlState.Normal);

            _button.SetStyleId("button1");

            _button.TouchUpInside += (sender, e) => _button.SetTitle(String.Format("clicked {0} times", _numClicks++), UIControlState.Normal);

            _button.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin |
                UIViewAutoresizing.FlexibleBottomMargin;

            var pixateCssFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "protopad.css");
            
            File.WriteAllText(pixateCssFilePath, @"#button1 {
    border-radius    : 5px;
    font-family      : ""American Typewriter"";
    font-size        : 13px;
    font-weight      : bold;
    text-transform   : uppercase;
    letter-spacing   : 0.75px;
    color            : #ffffff;
    background-color : #008ed4;
}");

            var styleSheet = PXEngine.StyleSheetFromFilePathWithOrigin(pixateCssFilePath, PXStylesheetOrigin.PXStylesheetOriginApplication);
            styleSheet.MonitorChanges = true;

            // Create the ProtoPad server and start listening for messages from the ProtoPad Client.
            _protoPadServer = ProtoPadServer.Create(UIApplication.SharedApplication.Delegate, UIApplication.SharedApplication.KeyWindow);
            _protoPadServer.AddPixateCssPath(pixateCssFilePath);

            // And that's it! 
            // ProtoPad Client should be able to automatically discover this app on your local wifi network (through multicast discovery)
            // The ProtoPad Client will be able to issue any code to this app, and that code will be able to inspect and alter your activity to any extent.
            // So get prototyping :-)

            var ipAddressLabel = new UILabel(View.Bounds)
            {
                Text = string.Format("Connect ProtoPad Client to me on {0}:{1}", _protoPadServer.LocalIPAddress, _protoPadServer.ListeningPort),
                LineBreakMode = UILineBreakMode.WordWrap,
                Lines = 0
            };
            View.AddSubview(ipAddressLabel);

            View.AddSubview(_button);
        }
    }
}