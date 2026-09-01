using Avalonia;

namespace LabInventario
{
    internal static class Program
    {
        // El punto de entrada NO debe usar tipos de Avalonia directamente en su
        // firma (recomendación oficial), para que el diseñador / previewer
        // puedan cargar el ensamblado sin inicializar la app completa.
        [STAThread]
        public static void Main(string[] args) =>
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
