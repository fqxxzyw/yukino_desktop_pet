using System;
using System.Windows;

namespace YukinoPet
{
    internal sealed class PetApplication : Application
    {
        [STAThread]
        public static void Main()
        {
            EmbeddedAssemblyLoader.Install();
            PetApplication app = new PetApplication();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            PetWindow window = new PetWindow(app);
            app.Run(window);
        }
    }
}
