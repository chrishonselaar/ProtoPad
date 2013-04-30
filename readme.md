# ProtoPad v0.01

A simple tool for [LIVE interactive development](https://github.com/chrishonselaar/ProtoPad#features) on iOS and Android.

## Getting started
ProtoPad consists of a very small dll (ProtoPad Server) that you include with your app, and a live code scratchpad (ProtoPad Client) that you can use to code against your app in real-time. You can use a completely blank app or an existing app. The ProtoPad server can be activated with one very simple statement:
ProtoPadServer.Create(this);
after which it will start listening for requests on your local network. When you fire up the ProtoPad Client, it will try to find a running app with ProtoPad Server active, and connect to it. That's it, now you can start coding live!
If you are familiar with [LinqPad](http://www.linqpad.net/), you will feel right at home. Use the editor to run any statements or even methods or entire classes on your device/simulator. They will execute immediately. You have full autocompletion functionality enabling you to easily discover the entire device coding framework. 
You can also send entire assemblies (as long as they are Xamarin.Android/Xamarin.iOS compatible respectively), that you can then use from your code.
You can use the .Dump() extension method to inspect any object or value - again, in your running app! The results will be visualized in a nice collapsible tree format. Enumerable contents are presented in full, in a special compact format (currently limited to 1000 items max). 
The .Dump() method works for Bitmaps/UIImages as well, and displays the image in the result pane.

Similar to LinqPad, use the dropdown in the toolbar to choose between modes of scripting: use 'C# Expression' to evaluate simple expressions/objects directly. You cannot use multiple statements in this mode and, and you should not end the line with a semicolon.
Use the default 'C# Statements' mode to enter one or multiple C# statements and run them together. You can use any valid C# statements.
Use 'C# Program' mode to enable writing functions and even classes.
When working connected to a mobile device or simulator, you can use the 'window' variable in your code to access the main application window directly. You can add and remove controls, add event handlers and generally do anything want within your app. You can even construct entire prototyped apps in this manner.

## License/usage
Currently using an MIT license. You are free to use the code for any purpose, including commercial.

## Contributions
Any valuable contributions to this project are extremely welcome!! A lot of work is necessary on the interface, cleaning up the code, planned features (see below), a Mac OS X version, etc. I would be very grateful for any and all help here! I am explicitly giving this project ot the community, so that it can get better.

## Important notes for iOS
ProtoPad for iOS works ONLY on the iOS simulator! Apple does not allow dynamic code to run on devices. But with help from the Xamarin.iOS/Mono runtime magic, ProtoPad is able to achieve dynamic code transfer and execution within the full simulator! This gives you an amazing ability to experiment with the entire iOS framework! You can quickly prototype entire apps this way, and without tedious compile/build/deploy cycles.
Once you connect to your iOS app on the simulator, ProtoPad Client will display the base file path for the app files within the simulator. Use this to quickly find your apps files so that you can inspect any files manipulated or read by your app (such as in the Documents or Library folders). You can set up a Samba share so you can access the files directly from your Windows machine (see screenshot). 
For my own workflow, I use a headless old Mac Mini (no monitor/keyboard/mouse) as the build server, and TeamViewer in unattended mode on the Mac so I can easily see the iOS simulator window. Also see screenshot, TeamViewer can automatically snap to the simulator window, so you do not lose space to the Mac desktop and can easily combine it on your screen with ProtoPad Client, Visual Studio, etc.
A Mac OS X version of ProtoPad Client is in the works as well.

## Important notes for Android
Not all Android devices support UDP Multicast (but most do). If your device does not support it, auto-discovery in ProtoPad Client will not find your app. No worries, the ProtoPadServer object will give you an IP Address and prot that you can fill in in ProtoPad Client and connect manually.
The Android emulator needs port forwarding set up in order to be able to connect to it on your local machine. This is done by logging in on the emulator through a Telnet client, and issuing port forwarding commands. ProtoPad client can take care of this for you automatically. Just use the 'New Android Emu' button. After that, you can use the Find button to automatically discover and connect to your app on the emulator.

## Requirements
* [Xamarin Android/iOS](http://xamarin.com/download) - the Starter/Free edition should work fine!
* A Windows machine (Windows Vista and up) - Mac OS X version is under development!

* For iOS you will also need a Mac OS X machine with Xamarin installed, as a build machine
Refer to the Xamarin website for installation guides:
http://docs.xamarin.com/guides/ios/getting_started/installation/windows
http://docs.xamarin.com/guides/android/getting_started/installation/windows

* If you want to build ProtoPad Client from the source, you will also need a copy of the ActiPro SyntaxEditor for WPF control, and the ActiPro SyntaxEditor .Net Addon. You can download a free 100% functional evaluation version here: http://www.actiprosoftware.com/products/controls/wpf/syntaxeditor

## Installation
Just clone this GitHub project, it includes everything you need and sample apps. 
Or you can download the ProtoPad client and assemblies to include in your iOS/Android app here: http://clearcode.nl/ProtoPad/client-pre-alpha.zip
Please check the Requirements section to ensure everything has been set up correctly.

## Troubleshooting
ProtoPad Client cannot find/connect to your ProtoPad Server-enabled app? Please try (temporarily) disabling your local firewall (on the Mac OS X build machine as well, for iOS), just to see if autodiscovery or data transfer might be hindered by this. Check out the source code to see which UDP/TCP ports are being used. You can override the main listening port on the app side by supplying it in the ProtoPadServer.Create call. Inspect the result of that function to find the IPAddress and port that are being used. You can use the "Manual IP" button in the ProtoPad client to connect to that address directly as well.
Also make sure that you enable all applicable permissions (internet/wifi/Multicast) in your app manifest/info.plist. Check out the sample apps for more information.

## Features
* Real-time interactive development / prototyping with full access to the entire c# language and .Net/Mono framework on mobile devices, including iPhone/iPad (simulator)!
* Ability to fully interact with running apps on a code-level, inspect their state directly without a debugger, without having to open Visual Studio or Xamarin Studio, and change their operation by mixing in new code on the fly. All without building, deploying, relaunching etc.. it’s immediate.
* Advanced code editor with full IntelliPrompt etc support for .Net/Xamarin.iOS/Xamarin.Android/etc frameworks (automatically deduced from the device you connect to)
* Friendly indication of compile- and runtime-errors
* Ability to inspect any object/value inline by using a “.Dump()” extension method – the result of which is presented as an interactive, collapsible nested data view that includes optimized displays for collections, objects and value types.
* Ability to Dump a Bitmap (or UIImage) and see this in ProtoPad’s interactive result view – very useful for image processing.
* Ability to send any (device framework compatible) assembly to the device, and have it be accessible from code you send immediately
* Auto-discovers running “Protopad-enabled” apps on devices/simulators on your local network (through network multicast functionality), and allows you to select and connect to them. Automatically sets up port forwarding on Android Emulators as well.
* A simple, light-weight assembly that you can use to add Protopad functionality to any of your existing .Net (iOS/Android/etc) apps, so that you can easily deep-inspect that app without breakpoints/pausing etc, and even modify it on the fly to rapidly prototype changes.
* Friendly statusbar indicator of the local filesystem location of the connected app (when running on a simulator), so that you can find and access your live running app’s files easily (see screenshot 1, having the OS/X simulator files location mapped to a Windows share makes it really easy to prototype functionality that interacts with the iOS filesystem!)
* Easily clear your app’s main view with one click (so you can do quick full interface make-overs)

## Planned features
* Improve the UI, add keyboard shortcuts, better documentation etc.
* Add Using Statements configuration
* ProtoPad editor/client app for Mac OS/X (Windows only for now, sorry, working on it!)
* Windows RT and Windows Phone compatibility
* Ability to connect to an SQLite database used by the app, inspect and change its data and schema on the fly
* Direct access to app files (useful for non-simulator sessions, or when you do not want to set up network shares for this)
* Multiple editor+result tabs
* Saving, Snippet repository and potentially integration with snippets on the Xamarin website?
* Full integration with the larger framework (working name 'Carnival') that I am developing – which allows you to do MVVM style development with automatic binding+dependency tracking, with a view abstraction layer that allows you to achieve 100% code reuse if desired across all supported platforms (iOS/Android/Windows RT/Windows Phone/HTML5+Javascript offline/more TBA).

### Potentially interesting stuff in the source code
If you are interested in any of the following topics, check out the source code - it provides some very easy to follow examples for these:
* Using the UDP broadcast protocol (also see mDNS) to automatically publish and locate your service on a local network
* Making UDP work on Android and iOS (acquiring a multicast lock, device-specific endpoint setup)
* Reflection for object state 'dumping'
* Simple Http async server on Android/iOS (without external dependencies)
* Using the Mshtml (Web Browser) control from WPF interactively
* Using the ActiPro Syntax Editor and its autocompletion, line error highlighting, and Abstract Syntax Tree functionality
* Using the CSharpCodeProvider class to quickly compile code snippets
* Using AppDomain, Activator and MethodInfo.Invoke to run ad hoc code (in the form of serialized assemblies)
* Using TelNet from C# for simple scenarios (used here for setting up port forwarding)
* Discovering active ports and their associated processes
