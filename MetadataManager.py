import itertools
import os
import subprocess
import threading
import time
from pathlib import Path
from tkinter import filedialog, Tk


def limpiar_pantalla():
    os.system("cls" if os.name == "nt" else "clear")

def getUserPath():
    return str(Path.home())

def seleccionarArchivo():
    root = Tk()
    root.attributes('-topmost', True)
    root.withdraw()
    archivo = filedialog.askopenfilename(title="Selecciona un archivo para ver los metadatos")
    root.destroy()
    return archivo

def verMetadatos(archivo):
    if archivo:
        resultado = subprocess.run(["exiftool", archivo], capture_output=True, text=True)
        salida_completa = resultado.stdout + resultado.stderr
        print(salida_completa)

        save = input("\n¿Quieres guardar los metadatos en un .txt (y/n)? ")
        if save.lower() == "y":
            nombre_archivo = input("Nombre del archivo (sin extensión): ").strip()
            if not nombre_archivo:
                nombre_archivo = "metadatos"
            ruta_guardado = f"{nombre_archivo}.txt"
            try:
                with open(ruta_guardado, "w", encoding="utf-8") as f:
                    f.write(salida_completa)
                print(f"\nMetadatos guardados en: {ruta_guardado}")
            except Exception as e:
                print(f"Error al guardar: {e}")
            input("\nPulsa ENTER para continuar...")
        else:
            input("\nPulsa ENTER para continuar...")
            limpiar_pantalla()
    else:
        input("No has seleccionado ningún archivo. (ENTER para continuar)")
        limpiar_pantalla()

def modMetadata(archivo):
    if archivo:
        limpiar_pantalla()
        print("MODIFICAR METADATOS")
                
        print("\n ARCHIVO")
        print("1. Nombre del archivo")                       # FileName

        print("\n AUTORÍA Y SOFTWARE")
        print("2. Autor o Creador")                          # Author, Artist, Creator
        print("3. Título")                                   # Title
        print("4. Software utilizado")                       # Software
        print("5. Comentario")                               # Comment

        print("\n FECHAS EXIF")
        print("6. Fecha de creación (EXIF)")                 # CreateDate
        print("7. Fecha de edición (EXIF)")                  # ModifyDate
        print("8. Fecha original de la imagen")              # DateTimeOriginal

        print("\n FECHAS DEL SISTEMA DE ARCHIVOS")
        print("9. Fecha de creación en disco")               # FileCreateDate
        print("10. Fecha del último acceso")                 # FileAccessDate
        print("11. Fecha de modificación en disco")          # FileModifyDate

        print("\n CÁMARA")
        print("12. Modelo de cámara")                        # CameraModelName
        print("13. Fabricante de cámara")                    # Make
        print("14. Orientación")                             # Orientation
        print("15. Resolución X")                            # XResolution
        print("16. Resolución Y")                            # YResolution

        print("\n GPS")
        print("17. GPS Altitud")                             # GPSAltitude
        print("18. GPS Latitud")                             # GPSLatitude
        print("19. GPS Longitud")                            # GPSLongitude
        print("20. Referencia Latitud (N/S)")                # GPSLatitudeRef
        print("21. Referencia Longitud (E/W)")               # GPSLongitudeRef
        print("22. Referencia Altitud (Sea Level)")          # GPSAltitudeRef

        print("\n DATOS TÉCNICOS")
        print("23. Tiempo de exposición")                    # ExposureTime
        print("24. Número F (apertura)")                     # FNumber
        print("25. ISO")                                     # ISO
        print("26. Flash (sí/no)")                           # Flash
        print("27. Longitud focal")                          # FocalLength
        print("28. Espacio de color")                        # ColorSpace
        print("29. Balance de blancos")                      # WhiteBalance
        print("30. Zoom digital")                            # DigitalZoomRatio

        print("\n OTROS")
        print("31. ID única de imagen")                      # ImageUniqueID
        print("32. País")                                    # Country
        print("33. Código de país")                          # CountryCode
        print("34. MCC Data")                                # MCC (requiere XMP personalizado o Comment)

        input("\nENTER para continuar...")
        
        
    else:
        input("No has seleccionado ningún archivo. (ENTER para continuar)")
        limpiar_pantalla()
        
           
def deleteMetadata(archivo):
    pass
        
def main():
    archivo = None
    while True:
        limpiar_pantalla()
        print("""\033[92m

███╗   ███╗███████╗████████╗ █████╗ ██████╗  █████╗ ████████╗ █████╗ 
████╗ ████║██╔════╝╚══██╔══╝██╔══██╗██╔══██╗██╔══██╗╚══██╔══╝██╔══██╗
██╔████╔██║█████╗     ██║   ███████║██║  ██║███████║   ██║   ███████║
██║╚██╔╝██║██╔══╝     ██║   ██╔══██║██║  ██║██╔══██║   ██║   ██╔══██║
██║ ╚═╝ ██║███████╗   ██║   ██║  ██║██████╔╝██║  ██║   ██║   ██║  ██║
╚═╝     ╚═╝╚══════╝   ╚═╝   ╚═╝  ╚═╝╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚═╝  ╚═╝
                                                                     
███╗   ███╗ █████╗ ███╗   ██╗ █████╗  ██████╗ ███████╗██████╗        
████╗ ████║██╔══██╗████╗  ██║██╔══██╗██╔════╝ ██╔════╝██╔══██╗       
██╔████╔██║███████║██╔██╗ ██║███████║██║  ███╗█████╗  ██████╔╝       
██║╚██╔╝██║██╔══██║██║╚██╗██║██╔══██║██║   ██║██╔══╝  ██╔══██╗       
██║ ╚═╝ ██║██║  ██║██║ ╚████║██║  ██║╚██████╔╝███████╗██║  ██║       
╚═╝     ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝  ╚═╝      
       github page : https://github.com/FARLOPITEC \033[0m             
        """)
        print(f"Archivo seleccionado: {archivo if archivo else 'NULL'}\n")
        print("1. Seleccionar archivo")
        print("2. Ver metadatos")
        print("3. Modificar metadatos")
        print("4. Borrar metadatos")
        print("5. Salir")
        opcion = input("--> ").strip()

        if opcion == "1":
            archivo = seleccionarArchivo()

        elif opcion == "2":
            verMetadatos(archivo)

        elif opcion == "3":
            modMetadata(archivo)

        elif opcion == "4":
            deleteMetadata(archivo)

        elif opcion == "5":
            print("Saliendo...")
            time.sleep(1)
            limpiar_pantalla()
            break

        else:
            input("Opción no válida. (ENTER para continuar)")
            limpiar_pantalla()

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nInterrupción del usuario. Saliendo...")
        time.sleep(1)
        limpiar_pantalla()
    except Exception as e:
        print(f"Error: {e}")
        time.sleep(1)
        limpiar_pantalla()
