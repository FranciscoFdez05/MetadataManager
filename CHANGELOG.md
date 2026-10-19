# Registro de cambios

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/); el proyecto sigue
[versionado semántico](https://semver.org/lang/es/).

## [2.0.0] - 2026-10-19

### Cambios importantes
- La aplicación requiere ahora **.NET 10** (versión de soporte largo). El soporte de .NET 8 termina
  el 10 de noviembre de 2026. El ejecutable autocontenido sigue sin necesitar nada instalado.

### Rendimiento
- ExifTool se mantiene abierto entre órdenes (`-stay_open`) con un pool de hasta 4 procesos. Diez
  ediciones seguidas pasan de ~4,6 s a ~60 ms. Si el modo persistente falla, o un valor contiene
  saltos de línea, se usa automáticamente un proceso por orden como antes.
- Las rutas de archivo con cualquier carácter Unicode se pasan a ExifTool en UTF-8.

### Añadido
- Nuevo icono: un documento con foto y una etiqueta de metadatos. Cada tamaño (16 a 256 px) se
  dibuja por separado y los pequeños usan una versión simplificada.
- Iconos de la barra de herramientas rediseñados y centrados sobre una rejilla común. Se dibujan al
  tamaño real en píxeles de la pantalla (y se redibujan al cambiar de monitor), así que ya no se ven
  borrosos con el escalado de Windows.
- Botón **Cancelar** en la barra de estado (y tecla `Esc`) para la limpieza y la exportación por lotes.
  Una copia limpia a medias se elimina al cancelar.

### Corregido
- Con la interfaz en inglés, los nombres de las propiedades, los tipos de archivo, los mensajes de
  error y los informes exportados seguían en español: la capa de servicios tiene ahora sus propios
  recursos (`CoreStrings`) en ambos idiomas.
- La fila de coordenadas y la del SHA-256 se identifican por una clave estable, no por su texto.
- El mensaje de error inesperado y el estado al cancelar una exportación usaban textos fijos o incorrectos.

### Seguridad
- Se fuerza `System.Security.Cryptography.Pkcs` 10.0.13, que corrige GHSA-2fmv-rv2c-59x7 y
  GHSA-846f-f764-76mx. La versión 8.0.1 vulnerable llegaba como dependencia de PDFsharp.

### Pruebas
- Las pruebas fijan el español como idioma, de modo que no dependen del idioma del equipo.
- Nuevas pruebas de traducción y de integración con ExifTool real (182 en total).

## [1.5.0] - 2026-10-19

### Rendimiento
- Las rutas añadidas se comprueban en segundo plano y la lista se rellena de una vez; añadir una
  carpeta con miles de archivos ya no congela la ventana.
- El tamaño de cada archivo se lee una vez: ordenar por tamaño no consulta el disco en cada comparación.
- La limpieza procesa varios archivos en paralelo (hasta 4) y ya no prepara las copias en el hilo
  de la interfaz.
- La exportación por lotes lee los metadatos en paralelo y escribe el informe directamente al disco.
- La tabla de propiedades se construye en bloque con estilos compartidos; se corrige una fuga de
  fuentes GDI que se producía en cada repintado. El filtro espera a que se deje de escribir.
- SHA-256 por bloques de 1 MB sin búfer intermedio; la orientación EXIF se reutiliza de los datos ya
  cargados en la limpieza y la vista previa.
- El recorrido recursivo de carpetas ya no entra en enlaces simbólicos ni uniones, que podían
  formar ciclos.

### Estructura
- Código reorganizado en `src/` y `tests/`, con una librería `MetadataManager.Core` sin
  dependencias de Windows Forms y la aplicación en `MetadataManager`.
- `MainForm` dividido en archivos parciales por área funcional.
- Configuración común en `Directory.Build.props`, versiones de paquetes centralizadas en
  `Directory.Packages.props`, `global.json` y `.editorconfig`.
- La integración continua compila y prueba la solución completa.

### Pruebas
- Nuevas pruebas para `FileScanner`, `FileEntry` y la lectura en paralelo (171 en total).
