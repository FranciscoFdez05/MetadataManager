# 🧠 MetadataManager
[🇪🇸 Español](docs/README-ES.md) | [🇬🇧 English](README.md)

Aplicación en Python que permite visualizar, modificar y eliminar metadatos de archivos mediante una interfaz por consola. Utiliza `exiftool` para manipular los metadatos y `tkinter` para seleccionar archivos desde una ventana emergente. Ideal para fotógrafos, desarrolladores o cualquier persona que necesite gestionar metadatos de forma sencilla.

![](../img/menu.png)

## ⚠️ Aviso legal

Este software se proporciona con fines educativos y personales. **No me hago responsable del uso que otros puedan hacer de esta herramienta**. Cualquier modificación, eliminación o alteración de metadatos queda bajo la entera responsabilidad del usuario.

## ✨ Características ✨

- 📁 Selección gráfica del archivo con `tkinter`
- 🔍 Visualización detallada de metadatos usando `exiftool`
- 📝 Edición individual de campos EXIF, GPS, fecha, cámara, software, etc.
- ❌ Borrado completo de metadatos de cualquier archivo compatible
- 💾 Opción para guardar los metadatos en un archivo `.txt`
- 🎨 Menú interactivo en consola con diseño limpio

## 🖥️ Requisitos

- ✅ Python 3.7 o superior
- ✅ [ExifTool](https://exiftool.org/) instalado y accesible desde la línea de comandos:
	- **Windows**:  
	    Descargar e instalar desde [exiftool.org](https://exiftool.org/)
	- **Linux**:  
	    ```bash
	    sudo apt install libimage-exiftool-perl
	    ```
	- **macOS**:  
	    ```bash
	    brew install exiftool
	    ```
	 
- ✅ Módulo `tkinter` (incluido por defecto en la mayoría de distribuciones de Python)



## 📦 Guía de instalación ⚙️
1. Clona el repositorio:
   ```bash
   git clone https://github.com/FARLOPITEC/MetadataManager.git
   cd MetadataManager
   ```

2. Instala los requisitos de Python (si fuera necesario):
   ```bash
   pip install tk
   ```

3. Asegúrate de tener `exiftool` instalado (ver sección de requisitos).



## 📋 Guía de uso 🕹️

1. Ejecuta el script principal:
   ```bash
   python MetadataManager.py
   ```

2. Desde el menú principal podrás:
   - Seleccionar un archivo
   - Ver sus metadatos
   - Editar cualquier campo individual
   - Eliminar todos los metadatos del archivo
   - Guardar una copia de los metadatos en `.txt`



## 🤝 Contribuciones 🤝

¡Las contribuciones son bienvenidas! Si encuentras un error o tienes una mejora en mente:
- Haz un fork del repositorio
- Crea una rama
- Abre un pull request



## 📜 Licencia

📄 Este proyecto está licenciado bajo la Licencia MIT. Consulta el archivo [LICENSE](../LICENSE) para más detalles..  

**Developed with ❤️ by [Francisco](https://github.com/FARLOPITEC)**
