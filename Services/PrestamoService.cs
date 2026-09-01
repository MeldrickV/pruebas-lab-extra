using LabInventario.Data;
using LabInventario.Models;

namespace LabInventario.Services
{
    /// <summary>
    /// Resultado de registrar una salida: útil para que la GUI muestre un
    /// mensaje de éxito sin tener que volver a consultar la base de datos.
    /// </summary>
    public record ResultadoSalida(Alumno Alumno, Material Material, int PrestamoId);

    public record ResultadoEntrada(Alumno Alumno, Material Material, Prestamo Prestamo);

    /// <summary>
    /// Capa de servicios: aquí vive la lógica de negocio real (reglas de
    /// stock, validaciones), separada de la GUI y del acceso a datos.
    /// La GUI solo llama a estos métodos y reacciona a su resultado o a la
    /// excepción <see cref="PrestamoException"/> que puedan lanzar.
    /// </summary>
    public class PrestamoService
    {
        private readonly AlumnoRepository _alumnoRepo = new();
        private readonly MaterialRepository _materialRepo = new();
        private readonly PrestamoRepository _prestamoRepo = new();

        /// <summary>
        /// Flujo principal de escaneo → salida de material.
        /// </summary>
        /// <param name="fechaSalida">
        /// Fecha/hora a registrar. Cuando se procesa un lote de varios
        /// artículos escaneados para el mismo alumno (ver
        /// <c>OperacionControl</c>), el llamador debe fijar una sola
        /// <see cref="DateTime"/> y pasarla en todas las llamadas del lote,
        /// para que todo el préstamo quede registrado a la misma hora en
        /// vez de con pequeñas diferencias de milisegundos entre artículo y
        /// artículo. Si se omite, se usa la hora actual.
        /// </param>
        public ResultadoSalida RegistrarSalida(string numeroCuenta, string codigoBarras, int cantidad = 1, DateTime? fechaSalida = null)
        {
            var alumno = _alumnoRepo.ObtenerPorCuenta(numeroCuenta)
                ?? throw new PrestamoException(
                    $"No existe ningún alumno registrado con el número de cuenta '{numeroCuenta}'.");

            var material = _materialRepo.ObtenerPorCodigo(codigoBarras)
                ?? throw new PrestamoException(
                    $"No existe ningún material registrado con el código '{codigoBarras}'.");

            if (cantidad <= 0)
                throw new PrestamoException("La cantidad a prestar debe ser mayor que cero.");

            // --- Verificación automática de existencia (requisito clave) ---
            if (material.CantidadDisponible < cantidad)
            {
                throw new PrestamoException(
                    $"Stock insuficiente de '{material.Nombre}'. " +
                    $"Disponible: {material.CantidadDisponible}, solicitado: {cantidad}.");
            }

            var fecha = fechaSalida ?? DateTime.Now;
            _materialRepo.AjustarDisponible(material.Id, -cantidad);
            var prestamoId = _prestamoRepo.Crear(alumno.Id, material.Id, cantidad, fecha);

            material.CantidadDisponible -= cantidad; // reflejar el cambio en el objeto en memoria
            return new ResultadoSalida(alumno, material, prestamoId);
        }

        /// <summary>
        /// Flujo principal de escaneo → devolución de material. Igual que
        /// en <see cref="RegistrarSalida"/>, <paramref name="fechaRegreso"/>
        /// permite que un lote completo de devoluciones quede con la misma
        /// marca de tiempo.
        /// </summary>
        public ResultadoEntrada RegistrarEntrada(string numeroCuenta, string codigoBarras, DateTime? fechaRegreso = null)
        {
            var alumno = _alumnoRepo.ObtenerPorCuenta(numeroCuenta)
                ?? throw new PrestamoException(
                    $"No existe ningún alumno registrado con el número de cuenta '{numeroCuenta}'.");

            var material = _materialRepo.ObtenerPorCodigo(codigoBarras)
                ?? throw new PrestamoException(
                    $"No existe ningún material registrado con el código '{codigoBarras}'.");

            var prestamo = _prestamoRepo.BuscarActivo(alumno.Id, material.Id)
                ?? throw new PrestamoException(
                    $"No se encontró un préstamo activo de '{material.Nombre}' a nombre de {alumno.Nombre}.");

            var fecha = fechaRegreso ?? DateTime.Now;
            _prestamoRepo.MarcarDevuelto(prestamo.Id, fecha);
            _materialRepo.AjustarDisponible(material.Id, prestamo.Cantidad);

            prestamo.FechaRegreso = fecha;
            prestamo.Estado = EstadoPrestamo.Devuelto;
            material.CantidadDisponible += prestamo.Cantidad;

            return new ResultadoEntrada(alumno, material, prestamo);
        }
    }
}
