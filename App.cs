using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using LabInventario.Services;
using LabInventario.Windows;
using LabInventario.Helpers;

namespace LabInventario
{
    /// <summary>
    /// Clase de aplicación. Se construye toda en C# (sin .axaml) para
    /// mantener el mismo estilo "todo en código" que tenía la versión
    /// original en Windows Forms.
    ///
    /// Reproduce el ciclo que antes vivía en Program.cs: se muestra
    /// LoginWindow, y si se entra correctamente se abre MainWindow. Si
    /// desde el menú "Sesión" se elige "Cerrar sesión", MainWindow se
    /// cierra y el ciclo vuelve a mostrar el login; si se cierra de
    /// cualquier otra forma (o se cancela el login), la aplicación termina.
    /// </summary>
    public class App : Application
    {
        public override void Initialize()
        {
            // Equivalente a ApplicationConfiguration.Initialize() de WinForms:
            // registra el tema visual (Fluent) que usarán todas las ventanas.
            Styles.Add(new FluentTheme());
            Styles.Add(new Avalonia.Controls.DataGridFluentTheme());
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) Errores.RegistrarEnArchivo(ex);
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Errores.RegistrarEnArchivo(e.Exception);
                e.SetObserved();
            };
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Nosotros controlamos manualmente cuándo termina la app
                // (al cerrar el login o al cerrar MainWindow sin pedir
                // "cerrar sesión"), en vez de que Avalonia la cierre sola
                // al cerrarse la primera ventana.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _ = EjecutarCicloDeSesionAsync(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async Task EjecutarCicloDeSesionAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            while (true)
            {
                var login = new LoginWindow();
                desktop.MainWindow = login;
                login.Show();
                await EsperarCierre(login);

                if (!login.Aceptado)
                {
                    desktop.Shutdown();
                    return;
                }

                SesionActual.Rol = login.RolSeleccionado;

                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                await EsperarCierre(mainWindow);

                if (!mainWindow.SolicitoCerrarSesion)
                {
                    desktop.Shutdown();
                    return;
                }
                // Si se pidió cerrar sesión, el ciclo vuelve a mostrar el login.
            }
        }

        private static Task EsperarCierre(Window ventana)
        {
            var tcs = new TaskCompletionSource();
            ventana.Closed += (_, _) => tcs.TrySetResult();
            return tcs.Task;
        }
    }
}
