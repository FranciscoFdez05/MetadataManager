import os
import subprocess
import time
from tkinter import filedialog, Tk

def clearScreen():
    os.system("cls" if os.name == "nt" else "clear")

def selectFile():
    root = Tk()
    root.attributes('-topmost', True)
    root.withdraw()
    file = filedialog.askopenfilename(title="Select a file to view metadata")
    root.destroy()
    return file

def viewMetadata(file):
    if file:
        result = subprocess.run(["exiftool", file], captureOutput=True, text=True)
        fullOutput = result.stdout + result.stderr
        print(fullOutput)

        save = input("\nDo you want to save the metadata to a .txt file? (y/n) ")
        if save.lower() == "y":
            fileName = input("File name (without extension): ").strip()
            if not fileName:
                fileName = "metadata"
            savePath = f"{fileName}.txt"
            try:
                with open(savePath, "w", encoding="utf-8") as f:
                    f.write(fullOutput)
                print(f"\nMetadata saved to: {savePath}")
            except Exception as e:
                print(f"Error saving: {e}")
            input("\nPress ENTER to continue...")
        else:
            input("\nPress ENTER to continue...")
            clearScreen()
    else:
        input("No file selected. (Press ENTER to continue)")
        clearScreen()

def modMetadata(file):
    if not file:
        input("No file selected. (Press ENTER to continue)")
        clearScreen()
        return

    exifFields = {
        1: "FileName", 2: "Author", 3: "Title", 4: "Software", 5: "Comment",
        6: "CreateDate", 7: "ModifyDate", 8: "DateTimeOriginal",
        9: "FileCreateDate", 10: "FileAccessDate", 11: "FileModifyDate",
        12: "CameraModelName", 13: "Make", 14: "Orientation", 15: "XResolution", 16: "YResolution",
        17: "GPSAltitude", 18: "GPSLatitude", 19: "GPSLongitude",
        20: "GPSLatitudeRef", 21: "GPSLongitudeRef", 22: "GPSAltitudeRef",
        23: "ExposureTime", 24: "FNumber", 25: "ISO", 26: "Flash", 27: "FocalLength",
        28: "ColorSpace", 29: "WhiteBalance", 30: "DigitalZoomRatio",
        31: "ImageUniqueID", 32: "Country", 33: "CountryCode", 34: "MCC"
    }

    while True:
        clearScreen()
        print("EDIT METADATA")

        print("\n AUTHOR & SOFTWARE")
        print("1.  File name                    4.  Software used")
        print("2.  Author or creator            5.  Comment")
        print("3.  Title")

        print("\n EXIF DATES")
        print("6.  Creation date (EXIF)         9.  File creation date")
        print("7.  Modification date (EXIF)     10. Last access date")
        print("8.  Original image date          11. File modification date")

        print("\n CAMERA")
        print("12. Camera model                 13. Manufacturer")
        print("14. Orientation                  15. X resolution")
        print("16. Y resolution")

        print("\n GPS")
        print("17. GPS Altitude                 18. GPS Latitude")
        print("19. GPS Longitude                20. Latitude Ref (N/S)")
        print("21. Longitude Ref (E/W)          22. Altitude Ref")

        print("\n TECHNICAL DATA")
        print("23. Exposure time                26. Flash (yes/no)")
        print("24. F-Number (aperture)          27. Focal length")
        print("25. ISO                          28. Color space")
        print("29. White balance                30. Digital zoom ratio")

        print("\n OTHER")
        print("31. Unique image ID              32. Country")
        print("33. Country code                 34. MCC Data")

        print("\n OPTIONS")
        print("35. View metadata                36. Exit")

        option = input("\nSelect an option (1-36): ")

        if option == "35":
            subprocess.run(["exiftool", file])
            input("\nPress ENTER to continue...")
        elif option == "36":
            print("Exiting...")
            break
        elif option.isdigit() and int(option) in exifFields:
            field = exifFields[int(option)]

            formats = {
                "CreateDate": "YYYY:MM:DD HH:MM:SS",
                "ModifyDate": "YYYY:MM:DD HH:MM:SS",
                "DateTimeOriginal": "YYYY:MM:DD HH:MM:SS",
                "FileCreateDate": "YYYY:MM:DD HH:MM:SS+TZD",
                "FileAccessDate": "YYYY:MM:DD HH:MM:SS+TZD",
                "FileModifyDate": "YYYY:MM:DD HH:MM:SS+TZD",
                "GPSLatitude": "37.7749N or 37 deg 46' 29.64\" N",
                "GPSLongitude": "122.4194W or 122 deg 25' 9.84\" W",
                "GPSAltitude": "1234.56",
                "Flash": "0 (no) or 1 (yes)",
                "ISO": "100, 200, 400, etc.",
                "Orientation": "1-8",
                "XResolution": "72",
                "WhiteBalance": "0 (Auto), 1 (Manual)"
            }

            result = subprocess.run(
                ["exiftool", f"-{field}", file],
                captureOutput=True, text=True
            )

            output = result.stdout.strip()
            valorActual = output.split(": ", 1)[1] if ": " in output else "(no value)"

            print(f"\nCurrent value of '{field}': {valorActual}")

            if field in formats:
                print(f"Suggested format: {formats[field]}")

            newValue = input(f"New value for '{field}' (q to cancel): ")
            if newValue.lower() == "q":
                print("Changes cancelled.")
                input("\nPress ENTER to continue...")
                continue

            if field == "FileName":
                print("Modifying 'FileName' will rename the file.")
                confirm = input("Are you sure? (y/n): ")
                if confirm.lower() != "y":
                    print("Changes cancelled.")
                    input("\nPress ENTER to continue...")
                    continue

            subprocess.run(["exiftool", "-overwrite_original", f"-{field}={newValue}", file])
            print("Metadata updated.")
            input("\nPress ENTER to continue...")

        else:
            input("Invalid option. (Press ENTER to continue)")

def deleteMetadata(file):
    if file:
        subprocess.run(["exiftool", "-all=", file])
        print("Metadata deleted.")
        input("Press ENTER to continue...")
    else:
        input("No file selected. (Press ENTER to continue)")
        clearScreen()

def main():
    file = None
    while True:
        clearScreen()
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
        print(f"\nSelected file: {file if file else 'NULL'}\n")
        print("1. Select file")
        print("2. View metadata")
        print("3. Edit metadata")
        print("4. Delete metadata")
        print("5. Exit")
        option = input("--> ").strip()

        if option == "1":
            file = selectFile()
        elif option == "2":
            viewMetadata(file)
        elif option == "3":
            modMetadata(file)
        elif option == "4":
            deleteMetadata(file)
        elif option == "5":
            print("Exiting...")
            time.sleep(1)
            clearScreen()
            break
        else:
            input("Invalid option. (Press ENTER to continue)")
            clearScreen()

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nUser interruption. Exiting...")
        time.sleep(1)
        clearScreen()
    except Exception as e:
        print(f"Error: {e}")
        time.sleep(1)
        clearScreen()
