# Book Translator Studio

Aplicación WPF para cargar, extraer, traducir y exportar libros PDF.

## Versión actual

`v0.2.0`

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

## Alcance de v0.2.0

- Conteo real de páginas con PdfPig.
- Extracción de texto página por página.
- Previsualización del texto extraído.
- Cantidad de páginas con texto y caracteres totales.
- Identificación inicial de documentos que necesitarán OCR.
- Lectura asíncrona para mantener la interfaz disponible.
- Registro de resultados y errores.

Los PDFs compuestos únicamente por imágenes se marcarán para OCR, capacidad que se incorporará en una etapa posterior.
