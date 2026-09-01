using Microsoft.Data.Sqlite;

namespace LabInventario.Data
{
    /// <summary>
    /// Punto único de acceso a la base de datos SQLite local.
    /// Es un Singleton: crea el archivo .db y el esquema una sola vez al
    /// arrancar la aplicación, y todos los repositorios piden conexiones
    /// a través de él (Microsoft.Data.Sqlite maneja bien conexiones cortas
    /// y frecuentes, así que no se mantiene una conexión abierta permanente).
    /// </summary>
    public sealed class DatabaseManager
    {
        private static readonly Lazy<DatabaseManager> _instancia = new(() => new DatabaseManager());
        public static DatabaseManager Instancia => _instancia.Value;

        public string DbPath { get; }

        private DatabaseManager()
        {
            var carpetaDatos = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(carpetaDatos);
            DbPath = Path.Combine(carpetaDatos, "laboratorio.db");
            CrearEsquema();
        }

        /// <summary>Abre y devuelve una nueva conexión lista para usarse (con foreign keys activas).</summary>
        public SqliteConnection ObtenerConexion()
        {
            var conexion = new SqliteConnection($"Data Source={DbPath}");
            conexion.Open();
            using var pragma = conexion.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
            return conexion;
        }

        private void CrearEsquema()
        {
            using var conexion = ObtenerConexion();
            using var comando = conexion.CreateCommand();
            comando.CommandText = @"
                CREATE TABLE IF NOT EXISTS alumnos (
                    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre         TEXT NOT NULL,
                    NumeroCuenta   TEXT NOT NULL UNIQUE
                );

                CREATE TABLE IF NOT EXISTS materiales (
                    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                    CodigoBarras        TEXT NOT NULL UNIQUE,
                    Nombre              TEXT NOT NULL,
                    CantidadTotal       INTEGER NOT NULL DEFAULT 0,
                    CantidadDisponible  INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS prestamos (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    AlumnoId      INTEGER NOT NULL,
                    MaterialId    INTEGER NOT NULL,
                    Cantidad      INTEGER NOT NULL,
                    FechaSalida   TEXT NOT NULL,
                    FechaRegreso  TEXT NULL,
                    Estado        TEXT NOT NULL DEFAULT 'Activo',
                    FOREIGN KEY (AlumnoId) REFERENCES alumnos(Id),
                    FOREIGN KEY (MaterialId) REFERENCES materiales(Id)
                );

                CREATE INDEX IF NOT EXISTS idx_prestamos_estado   ON prestamos(Estado);
                CREATE INDEX IF NOT EXISTS idx_prestamos_alumno   ON prestamos(AlumnoId);
                CREATE INDEX IF NOT EXISTS idx_prestamos_material ON prestamos(MaterialId);

                -- Pares clave/valor de configuración general: contraseña de
                -- administrador (hash + salt) y patrón de detección de
                -- número de cuenta para el escaneo unificado.
                CREATE TABLE IF NOT EXISTS configuracion (
                    Clave  TEXT PRIMARY KEY,
                    Valor  TEXT NOT NULL
                );
            ";
            comando.ExecuteNonQuery();
        }
    }
}
