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
    pass
           
def deleteMetadata(archivo):
    pass
        
def main():
    archivo = None
    while True:
        limpiar_pantalla()
        print(r"""
    [+]-----------------------------------------------------------[+] 
    |    __  __ ______ _______       _____       _______           | 
    |   |  \/  |  ____|__   __|/\   |  __ \   /\|__   __|/\        | 
    |   | \  / | |__     | |  /  \  | |  | | /  \  | |  /  \       | 
    |   | |\/| |  __|    | | / /\ \ | |  | |/ /\ \ | | / /\ \      | 
    |   | |  | | |____   | |/ ____ \| |__| / ____ \| |/ ____ \     | 
    |   |_|  |_|______|  |_/_/    \_\_____/_/ ___\_\_/_/__  \_\    |
    |    __  __          _   _          __________________         | 
    |   |  \/  |   /\   | \ | |   /\   / ____|  ____|  __ \        | 
    |   | \  / |  /  \  |  \| |  /  \ | |  __| |__  | |__) |       | 
    |   | |\/| | / /\ \ | . ` | / /\ \| | |_ |  __| |  _  /        | 
    |   | |  | |/ ____ \| |\  |/ ____ \ |__| | |____| | \ \        | 
    |   |_|  |_/_/    \_\_| \_/_/    \_\_____|______|_|  \_\       |
    |                                                              |
    |       github page : https://github.com/FARLOPITEC            | 
    [+]-----------------------------------------------------------[+]
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
