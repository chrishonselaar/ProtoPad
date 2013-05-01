using System;
using System.IO;
using System.Windows;
using ActiproSoftware.Text.Languages.DotNet.Reflection;
using ActiproSoftware.Text.Languages.DotNet.Reflection.Implementation;
using ActiproSoftware.Text.Parsing;
using ActiproSoftware.Text.Parsing.Implementation;

namespace ProtoPad_Client
{
    public partial class App
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            AmbientParseRequestDispatcherProvider.Dispatcher = new ThreadedParseRequestDispatcher();

            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ClearCode\ProtoPad\Assembly Repository");
            AmbientAssemblyRepositoryProvider.Repository = new FileBasedAssemblyRepository(appDataPath);

        }

        protected override void OnExit(ExitEventArgs e) 
        {
            var repository = AmbientAssemblyRepositoryProvider.Repository;
            if (repository != null) repository.PruneCache();

            var dispatcher = AmbientParseRequestDispatcherProvider.Dispatcher;
            if (dispatcher == null) return;
            AmbientParseRequestDispatcherProvider.Dispatcher = null;
            dispatcher.Dispose();
        }
    }
}