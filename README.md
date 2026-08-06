# Book Translator Studio

Aplicación WPF para cargar, analizar, traducir y exportar libros PDF.

## Versión actual

`v0.1.0`

## Requisitos

- Windows 10 u 11 de 64 bits
- .NET SDK 10.0.302 o compatible

## Compilar

```powershell
dotnet restore
dotnet build BookTranslatorStudio.sln -c Release
```

## Ejecutar

```powershell
dotnet run --project .\src\BookTranslatorStudio.App\BookTranslatorStudio.App.csproj
```

## Alcance de v0.1.0

- Ventana principal WPF.
- Selección de archivos PDF.
- Validación básica del encabezado PDF.
- Lectura de nombre, ruta, tamaño y cantidad estimada de páginas.
- Registro de errores en `%LocalAppData%\BookTranslatorStudio\Logs`.
- Arquitectura MVVM sin dependencias externas.

La extracción real del texto se incorporará en la siguiente etapa.
