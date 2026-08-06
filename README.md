# Book Translator Studio

Aplicación WPF para importar, preparar, editar, traducir y exportar libros.

## Versión actual

`v0.3.0`

## Resultado funcional

La aplicación permite completar en un solo flujo:

1. Importar un libro PDF.
2. Extraer todo el texto disponible.
3. Reconstruir automáticamente una estructura editable.
4. Navegar por secciones y bloques.
5. Corregir el texto original.
6. Preparar o editar la traducción correspondiente.
7. Guardar el libro como proyecto `.btsproject`.
8. Cerrar y reabrir el proyecto sin perder información.

## Compilar

```powershell
dotnet restore
dotnet build BookTranslatorStudio.sln -c Release
```

## Ejecutar

```powershell
dotnet run --project .\src\BookTranslatorStudio.App\BookTranslatorStudio.App.csproj
```
