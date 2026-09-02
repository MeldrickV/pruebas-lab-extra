using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using LabInventario.Data;
using LabInventario.Helpers;

namespace LabInventario.Views
{
    /// <summary>
    /// Pestaña de préstamos: historial completo con filtro de búsqueda y
    /// opción de mostrar solo los activos. También permite marcar
    /// manualmente un préstamo como devuelto (por si el material se
    /// regresa sin poder escanear el código).
    /// </summary>
    public class PrestamosView : UserControl
    {
        private class FilaPrestamo
        {
            public int Id { get; set; }
            public string Alumno { get; set; } = "";
            public string Cuenta { get; set; } = "";
            public string Material { get; set; } = "";
            public string Codigo { get; set; } = "";
            public int Cantidad { get; set; }
            public string Salida { get; set; } = "";
            public string Regreso { get; set; } = "";
            public string Estado { get; set; } = "";
            public int MaterialId { get; set; }
        }

        private readonly PrestamoRepository _repo = new();
        private readonly MaterialRepository _materialRepo = new();
        private readonly ObservableCollection<FilaPrestamo> _filas = new();
        private readonly DataGrid _grid;
        private readonly TextBox _txtFiltro = new() { Width = 260 };
        private readonly CheckBox _chkSoloActivos = new() { Content = "Mostrar solo préstamos activos" };

        public PrestamosView()
        {
            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserSortColumns = true,
                SelectionMode = DataGridSelectionMode.Single,
                ItemsSource = _filas,
                Columns =
                {
                    new DataGridTextColumn { Header = "Alumno", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Alumno)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Cuenta", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Cuenta)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Material", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Material)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Código", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Codigo)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Cant.", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Cantidad)), Width = new DataGridLength(0.6, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Fecha salida", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Salida)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Fecha regreso", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Regreso)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Estado", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Estado)), Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) },
                },
            };

            _txtFiltro.TextChanged += (_, _) => Cargar();
            _chkSoloActivos.PropertyChanged += (_, e) => { if (e.Property == ToggleButton.IsCheckedProperty) Cargar(); };

            var btnDevolver = new Button { Content = "Marcar como devuelto", Width = 180 };
            btnDevolver.Click += (_, _) => Errores.Ejecutar(VentanaPropietaria(), MarcarDevuelto);

            var panelSuperior = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
            };
            panelSuperior.Children.Add(new TextBlock { Text = "Buscar:", VerticalAlignment = VerticalAlignment.Center });
            panelSuperior.Children.Add(_txtFiltro);
            panelSuperior.Children.Add(_chkSoloActivos);
            panelSuperior.Children.Add(btnDevolver);

            var raiz = new DockPanel { Margin = new Avalonia.Thickness(15) };
            DockPanel.SetDock(panelSuperior, Dock.Top);
            raiz.Children.Add(panelSuperior);
            raiz.Children.Add(_grid);

            Content = raiz;
            Cargar();
        }

        private void Cargar()
        {
            _filas.Clear();
            foreach (var p in _repo.ListarDetallado(_txtFiltro.Text?.Trim() ?? "", _chkSoloActivos.IsChecked == true))
            {
                _filas.Add(new FilaPrestamo
                {
                    Id = p.Id,
                    Alumno = p.AlumnoNombre,
                    Cuenta = p.NumeroCuenta,
                    Material = p.MaterialNombre,
                    Codigo = p.CodigoBarras,
                    Cantidad = p.Cantidad,
                    Salida = p.FechaSalida.ToString("yyyy-MM-dd HH:mm:ss"),
                    Regreso = p.FechaRegreso?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-",
                    Estado = p.Estado,
                    MaterialId = p.MaterialId,
                });
            }
        }

        /// <summary>Recarga los datos desde la base de datos. Se llama cada vez que esta pestaña se vuelve visible.</summary>
        public void Actualizar() => Cargar();

        private Window? VentanaPropietaria() =>
            (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        private async Task MarcarDevuelto()
        {
            var propietaria = VentanaPropietaria();
            if (propietaria is null) return;

            var seleccionado = _grid.SelectedItem as FilaPrestamo;
            if (seleccionado is null)
            {
                await Dialogos.MostrarInfo(propietaria, "Elige un préstamo de la tabla primero.", "Sin selección");
                return;
            }

            if (seleccionado.Estado == "Devuelto")
            {
                await Dialogos.MostrarInfo(propietaria, "Este préstamo ya fue devuelto.", "Sin cambios");
                return;
            }

            var confirmar = await Dialogos.Confirmar(propietaria,
                "¿Marcar este préstamo como devuelto y regresar el material al stock disponible?",
                "Confirmar devolución");
            if (!confirmar) return;

            var prestamo = _repo.ObtenerPorId(seleccionado.Id)!;
            _repo.MarcarDevuelto(seleccionado.Id, DateTime.Now);
            _materialRepo.AjustarDisponible(prestamo.MaterialId, prestamo.Cantidad);
            Cargar();
        }
    }
}
