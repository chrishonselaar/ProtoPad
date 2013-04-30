using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace Pixate_Sample_iOS
{
    [Register("AppDelegate")]
    public class AppDelegate : UIApplicationDelegate
    {
        UIWindow _window;
        MyViewController _viewController;

        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            _window = new UIWindow(UIScreen.MainScreen.Bounds);

            _viewController = new MyViewController();
            _window.RootViewController = _viewController;

            _window.MakeKeyAndVisible();

            return true;
        }
    }
}