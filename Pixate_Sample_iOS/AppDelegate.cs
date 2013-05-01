using System;
using System.IO;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using PixateLib;
using ProtoPadServerLibrary_iOS;
using System.Drawing;

namespace Pixate_Sample_iOS
{
    [Register("AppDelegate")]
    public class AppDelegate : UIApplicationDelegate
    {
        UIWindow _window;
        MyViewController _viewController;
        private ProtoPadServer _protoPadServer;

        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            _window = new UIWindow(UIScreen.MainScreen.Bounds);

            _viewController = new MyViewController();
            _window.RootViewController = _viewController;

            _window.MakeKeyAndVisible();


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
            _protoPadServer = ProtoPadServer.Create(this, _window);
            _protoPadServer.AddPixateCssPath(pixateCssFilePath);

            // And that's it! 
            // ProtoPad Client should be able to automatically discover this app on your local wifi network (through multicast discovery)
            // The ProtoPad Client will be able to issue any code to this app, and that code will be able to inspect and alter your activity to any extent.
            // So get prototyping :-)

            var ipAddressLabel = new UILabel(new RectangleF(0,0,_window.Bounds.Width, 100))
            {
                Text = string.Format("Connect ProtoPad Client to me on {0}:{1}", _protoPadServer.LocalIPAddress, _protoPadServer.ListeningPort),
                LineBreakMode = UILineBreakMode.WordWrap,
                Lines = 0,
            };
            _window.AddSubview(ipAddressLabel);

            return true;
        }
    }
}