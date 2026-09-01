# LabInventario — versión multiplataforma (Avalonia UI)

Esta es la migración del proyecto original de **Windows Forms** (`net8.0-windows`,
solo Windows) a **Avalonia UI** sobre **.NET 8** puro, que corre de forma nativa
en **Linux (Ubuntu)**, Windows y macOS.

## Qué cambió y qué no

- **Sin cambios**: `Models/`, `Data/`, `Services/` — toda la lógica de negocio,
  acceso a SQLite y detección de patrones se copió tal cual. Ya era código
  100% independiente de la plataforma.
- **Reescrito**: todo lo que antes vivía en `Forms/`, `Controls/` y `Dialogs/`
  (Windows Forms) ahora vive en `Windows/`, `Views/` y `Dialogs/` (Avalonia).
  El comportamiento y flujo de cada pantalla es el mismo; solo cambió la
  tecnología de UI subyacente.
- `DataGridView` → `Avalonia.Controls.DataGrid`
- `MessageBox` / `OpenFileDialog` (no existen en Avalonia) → helpers propios
  en `Helpers/UiHelpers.cs` (`Dialogos.MostrarInfo`, `Dialogos.Confirmar`,
  `Dialogos.SeleccionarArchivo`, etc.), usando `IStorageProvider` de forma
  asíncrona.
- `GroupBox` (no existe en Avalonia) → `Cajas.GroupBox(...)` en el mismo
  archivo de helpers.

## Requisitos en Ubuntu

```bash
# .NET 8 SDK (si no lo tienes)
sudo apt update
sudo apt install -y dotnet-sdk-8.0
```

Si tu Ubuntu no trae el paquete `dotnet-sdk-8.0` en sus repos, instala el SDK
siguiendo la guía oficial: https://learn.microsoft.com/dotnet/core/install/linux-ubuntu

## Compilar y ejecutar

```bash
cd LabInventario.Avalonia
dotnet restore
dotnet run
```

> **Nota:** este entorno donde se generó el código no tiene acceso a NuGet,
> así que el proyecto **no se pudo compilar aquí** para verificarlo de punta
> a punta. La lógica de cada pantalla es una traducción directa y cuidadosa
> del original (mismos flujos, mismas validaciones), pero corre
> `dotnet build` en tu máquina y avísame si algo no compila — lo ajusto.

## Publicar un ejecutable para Ubuntu

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable resultante queda en
`bin/Release/net8.0/linux-x64/publish/LabInventario`.

## Compilar automáticamente con GitHub Actions

El repo incluye dos workflows en `.github/workflows/`:

- **`build.yml`** — compila el proyecto en Ubuntu, Windows y macOS en cada
  push/PR a `main` (o a mano desde la pestaña **Actions**). Es la forma más
  rápida de confirmar que todo compila, ya que aquí no tuve acceso a NuGet
  para probarlo yo mismo.
- **`publish.yml`** — genera ejecutables autocontenidos (`linux-x64`,
  `win-x64`, `osx-x64`) descargables como *artifacts*. Se lanza a mano, o
  automáticamente al crear un tag `v*` (ej. `git tag v1.0.0 && git push
  --tags`), y en ese caso además los adjunta a un Release de GitHub.

Para usarlos: sube este proyecto a un repo de GitHub y entra a la pestaña
**Actions** — deberían dispararse solos en el primer push. Si `build.yml`
falla, el log del job te dirá la línea exacta del error para que lo
corrijamos.

## Base de datos

Igual que antes: al primer arranque se crea automáticamente
`data/laboratorio.db` (SQLite) junto al ejecutable, con usuario
administrador por defecto (contraseña `admin123`, cámbiala desde el menú
Administración).
