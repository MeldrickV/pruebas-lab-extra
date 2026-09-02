using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using LabInventario.Data;
using LabInventario.Dialogs;
using LabInventario.Helpers;
using LabInventario.Services;

namespace LabInventario.Views
{
    /// <summary>
    /// Pestaña de préstamos: historial completo con filtro de búsqueda y
    /// opción de mostrar solo los activos.
    ///
    /// Los préstamos ACTIVOS del mismo alumno+material se agrupan en una
    /// sola fila resumen (cantidad total pendiente y fecha del último
    /// escaneo), con una flecha para expandir y ver el detalle de cada
    /// registro individual por separado (útil cuando el alumno sacó el
    /// mismo material en días distintos). Los ya devueltos se muestran
    /// como filas sueltas, tal como quedaron en su momento.
    ///
    /// Al marcar una devolución, si se selecciona la fila-grupo, la
    /// cantidad indicada se descuenta en orden FIFO entre los registros
    /// agrupados (el más antiguo primero). Si se selecciona un registro
    /// específico dentro del detalle expandido, la devolución se aplica
    /// solo a ese registro puntual.
    /// </summary>
    public class PrestamosView : UserControl
    {
        private class FilaPrestamo
        {
            public bool EsGrupo { get; set; }
            public bool Expandido { get; set; }
            public int? PrestamoId { get; set; }
            public int AlumnoId { get; set; }
            public int MaterialId { get; set; }
            public string Alumno { get; set; } = "";
            public string Cuenta { get; set; } = "";
            public string Material { get; set; } = "";
            public string Codigo { get; set; } = "";
            public int Cantidad { get; set; }
            public string Salida { get; set; } = "";
            public string Regreso { get; set; } = "";
            public string Estado { get; set; } = "";
        }

        private readonly PrestamoRepository _repo = new();
        private readonly PrestamoService _servicio = new();
        private readonly ObservableCollection<FilaPrestamo> _filas = new();
        private readonly HashSet<(int AlumnoId, int MaterialId)> _gruposExpandidos = new();
        private readonly DataGrid _grid;
        private readonly TextBox _txtFiltro = new() { Width = 260 };
        private readonly CheckBox _chkSoloActivos = new() { Content = "Mostrar solo préstamos activos" };

        public PrestamosView()
        {
            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserSortColumns = false, // el orden lo define el agrupado; no se debe romper con un sort libre
                SelectionMode = DataGridSelectionMode.Single,
                ItemsSource = _filas,
                Columns =
                {
                    new DataGridTemplateColumn
                    {
                        Header = "",
                        Width = new DataGridLength(36),
                        CellTemplate = new FuncDataTemplate<FilaPrestamo>((fila, _) =>
                        {
                            if (fila is null || !fila.EsGrupo) return new TextBlock();
                            var boton = new Button
                            {
                                Content = fila.Expandido ? "▼" : "▶",
                                Padding = new Avalonia.Thickness(6, 0),
                                Background = Brushes.Transparent,
                                BorderThickness = new Avalonia.Thickness(0),
                            };
                            boton.Click += (_, _) => AlternarExpansion(fila.AlumnoId, fila.MaterialId);
                            return boton;
                        }),
                    },
                    new DataGridTextColumn { Header = "Alumno", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Alumno)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Cuenta", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Cuenta)), Width = new DataGridLength(0.9, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Material", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Material)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Código", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Codigo)), Width = new DataGridLength(0.9, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Cant.", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Cantidad)), Width = new DataGridLength(0.5, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Fecha salida", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Salida)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Fecha regreso", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Regreso)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) },
                    new DataGridTextColumn { Header = "Estado", Binding = new Avalonia.Data.Binding(nameof(FilaPrestamo.Estado)), Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) },
                },
            };

            _txtFiltro.TextChanged += (_, _) => Cargar();
            _chkSoloActivos.PropertyChanged += (_, e) => { if (e.Property == ToggleButton.IsCheckedProperty) Cargar(); };

            var btnDevolver = new Button { Content = "Marcar como devuelto", Width = 180 };
            btnDevolver.Click += (_, _) => Errores.Ejecutar(VentanaPropietaria(), MarcarDevuelto);

            var lblAyuda = new TextBlock
            {
                Text = "Toca ▶ para ver el detalle de cada salida por separado.",
                Foreground = Brushes.DimGray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(15, 0, 0, 0),
            };

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
            panelSuperior.Children.Add(lblAyuda);

            var raiz = new DockPanel { Margin = new Avalonia.Thickness(15) };
            DockPanel.SetDock(panelSuperior, Dock.Top);
            raiz.Children.Add(panelSuperior);
            raiz.Children.Add(_grid);

            Content = raiz;
            Cargar();
        }

        /// <summary>Recarga los datos desde la base de datos. Se llama cada vez que esta pestaña se vuelve visible.</summary>
        public void Actualizar() => Cargar();

        private void Cargar()
        {
            _filas.Clear();

            var detallados = _repo.ListarDetallado(_txtFiltro.Text?.Trim() ?? "", _chkSoloActivos.IsChecked == true);
            var activos = detallados.Where(p => p.Estado == "Activo").ToList();
            var devueltos = detallados.Where(p => p.Estado != "Activo").ToList();

            var grupos = activos
                .GroupBy(p => (p.AlumnoId, p.MaterialId))
                .Select(g => new
                {
                    g.Key.AlumnoId,
                    g.Key.MaterialId,
                    Alumno = g.First().AlumnoNombre,
                    Cuenta = g.First().NumeroCuenta,
                    Material = g.First().MaterialNombre,
                    Codigo = g.First().CodigoBarras,
                    Total = g.Sum(x => x.Cantidad),
                    UltimaSalida = g.Max(x => x.FechaSalida),
                    Detalle = g.OrderBy(x => x.FechaSalida).ToList(),
                })
                .OrderByDescending(g => g.UltimaSalida)
                .ToList();

            foreach (var grupo in grupos)
            {
                var expandido = _gruposExpandidos.Contains((grupo.AlumnoId, grupo.MaterialId));

                _filas.Add(new FilaPrestamo
                {
                    EsGrupo = true,
                    Expandido = expandido,
                    AlumnoId = grupo.AlumnoId,
                    MaterialId = grupo.MaterialId,
                    Alumno = grupo.Alumno,
                    Cuenta = grupo.Cuenta,
                    Material = grupo.Material,
                    Codigo = grupo.Codigo,
                    Cantidad = grupo.Total,
                    Salida = grupo.UltimaSalida.ToString("yyyy-MM-dd HH:mm:ss") + (grupo.Detalle.Count > 1 ? "  (última)" : ""),
                    Regreso = "-",
                    Estado = "Activo",
                });

                if (!expandido) continue;

                foreach (var item in grupo.Detalle)
                {
                    _filas.Add(new FilaPrestamo
                    {
                        EsGrupo = false,
                        PrestamoId = item.Id,
                        AlumnoId = grupo.AlumnoId,
                        MaterialId = grupo.MaterialId,
                        Alumno = "      ↳ detalle",
                        Cuenta = "",
                        Material = item.MaterialNombre,
                        Codigo = item.CodigoBarras,
                        Cantidad = item.Cantidad,
                        Salida = item.FechaSalida.ToString("yyyy-MM-dd HH:mm:ss"),
                        Regreso = "-",
                        Estado = "Activo",
                    });
                }
            }

            foreach (var p in devueltos)
            {
                _filas.Add(new FilaPrestamo
                {
                    EsGrupo = false,
                    PrestamoId = p.Id,
                    AlumnoId = p.AlumnoId,
                    MaterialId = p.MaterialId,
                    Alumno = p.AlumnoNombre,
                    Cuenta = p.NumeroCuenta,
                    Material = p.MaterialNombre,
                    Codigo = p.CodigoBarras,
                    Cantidad = p.Cantidad,
                    Salida = p.FechaSalida.ToString("yyyy-MM-dd HH:mm:ss"),
                    Regreso = p.FechaRegreso?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-",
                    Estado = p.Estado,
                });
            }
        }

        private void AlternarExpansion(int alumnoId, int materialId)
        {
            var clave = (alumnoId, materialId);
            if (!_gruposExpandidos.Add(clave))
                _gruposExpandidos.Remove(clave);
            Cargar();
        }

        private Window? VentanaPropietaria() =>
            (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        private async Task MarcarDevuelto()
        {
            var propietaria = VentanaPropietaria();
            if (propietaria is null) return;

            var seleccionado = _grid.SelectedItem as FilaPrestamo;
            if (seleccionado is null)
            {
                await Dialogos.MostrarInfo(propietaria, "Elige una fila de la tabla primero (el grupo o un detalle expandido).", "Sin selección");
                return;
            }

            if (seleccionado.Estado != "Activo")
            {
                await Dialogos.MostrarInfo(propietaria, "Este préstamo ya fue devuelto.", "Sin cambios");
                return;
            }

            var mensaje = seleccionado.EsGrupo
                ? $"¿Cuántos '{seleccionado.Material}' regresa {seleccionado.Alumno}? (de {seleccionado.Cantidad} pendientes en total; se descuenta primero de la salida más antigua)"
                : $"¿Cuántos regresan de este registro puntual? (de {seleccionado.Cantidad})";

            var dialogoCantidad = new CantidadDialog(mensaje, maximo: seleccionado.Cantidad, valorInicial: seleccionado.Cantidad);
            await dialogoCantidad.ShowDialog(propietaria);
            if (dialogoCantidad.Resultado is not int cantidad) return;

            if (seleccionado.EsGrupo)
                _servicio.RegistrarEntradaPorAlumnoYMaterial(seleccionado.AlumnoId, seleccionado.MaterialId, cantidad, DateTime.Now);
            else
                _servicio.RegistrarEntradaDePrestamoEspecifico(seleccionado.PrestamoId!.Value, cantidad, DateTime.Now);

            Cargar();
        }
    }
}
