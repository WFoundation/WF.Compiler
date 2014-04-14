#WF.Compiler

##What is the purpose of WF.Compiler?

With WF.Compiler, Wherigo GWZ files could be converted to Wherigo GWC files. It is 
modular and should capable to read and write different formats for future use. 

##What is a Wherigo GWZ file? 

A Wherigo GWZ file is a Zip file, which contains all files, that are belonging to 
a Wherigo cartridge. This are a Lua file with the source code of the cartridge and
no, one or many media files in different formats, that are used by the cartridge.

##What is a Wherigo GWC file?

A Whrigo GWC file is a file, which contains the binary chunk (compiled Lua source 
code) of the cartridge and no, one or many media files in the correct format for 
the engines, for which the GWZ files was compiled.

##What does the WF.Compiler?

The WF.Compiler exists of three parts:
1. Check: Extract Lua source code, check, if it is valid Lua code, and check, if 
all referenced files are in the GWZ file. 
2. Engine conversion: Change the Lua source code and the media files in a manner, 
that they are correct for the selected engine.
3. Creation: Create the GWC file in the correct format with all medias.

##What does the WF.Compiler while checking?

The WF.Compiler opens the GWZ as Zip file and search for the Lua file. If it is 
found, it is extracted and the Lua code is compiled with Lua 5.1.4 (original C 
implementation, which could be found at www.lua.org). If their are problems, an 
exception is thrown. If it is all right, we start the Lua source code in the Lua
environment with the Wherigo library active. After this, we informations for the
cartridge and the used medias. Than it is checked, if all medias, which are 
referenced in the Lua source code, existing in the GWZ file.

If there are no errors, the GWZ file is valid.

##What does the WF.Compiler while conversion?

While compiling, there could be selected a target device. This is one of the 
following device types:

- Unknown
- PPC2003
- Garmin
- Colorado
- WhereYouGo
- DesktopWIG
- OpenWIG
- XMarksTheSpot
- iPhone
- iPad
- iPhoneRetina
- iPadRetina
- Emulator
- WFPlayer
     
Each of this devices has different capabilities. That could be different image
or sound formats or differences in the Lua source code format. For example, 
Garmins could only handle JPGs, so all image formats are converted to 24-bit JPG.

###Garmin, Colorado:
To this group also the Oregon belongs. The following things are made by the 
conversion:
- Convert special characters in Urwigo from UTF-8 to Win-1252
- Convert characters in long and short strings 
  * "&" -> "&amp;"
  * "<" -> "&lt;"
  * ">" -> "&gt;"
  * "\t" -> "   "
  * two and more spaces -> "&nbsp;" for each space over one
  * "\r\n" or "\n\r" -> "\n"
  * "\r" or "\n" -> "&lt;BR&gt;\n"    
- Convert images other than JPG 24-bit to JPG 24-bit
- Resize images wider than 230 pixel without Directives containing "noresize" to 
  230 pixel width
- Remove all sound files with other format as FDL
- Insert workarounds for
  * ShowScreen bug (crash when a list should be shown))
  * GetInput bug (crash when an input is replaced by other screen)
  * Timer stop bug (not possible to timer stop in timer tick event) 
 
##What does the WF.Compiler while creation?

All selected informations are written to the GWC file in the valid format, so 
that the engines could read the file.

File format of GWC files:

    @0000:                          ; Signature
        BYTE     0x02               ; Version
        BYTE     0x0a               ; 2.10 or 2.11
        BYTE     "CART"
        BYTE     0x00

    @0007:
        USHORT   NumberOfObjects    ; Number of objects ("media files") in cartridge:

    @0009:
        ; References to individual objects in cartridge.
        ; Object 0 is always Lua bytecode for cartridge.
        ; There is exactly [number_of_objects] blocks like this:
        repeat <NumberOfObjects> times
        {
            USHORT   ObjectID       ; Distinct ID for each object, duplicates are forbidden
            INT      Address          ; Address of object in GWC file
        }

    @xxxx:                          ; 0009 + <NumberOfObjects> * 0006 bytes from begining
        ; Header with all important informations for this cartridge
        INT      HeaderLength       ; Length of information header (following block):

        DOUBLE   Latitude           ; N+/S-
        DOUBLE   Longitude          ; E+/W-
        DOUBLE   Altitude           ; Meters

        LONG     Date of creation   ; Seconds since 2004-02-10 01:00:00

        ; MediaID of icon and splashscreen
        SHORT    ObjectID of splashscreen    ; -1 = without splashscreen/poster
        SHORT    ObjectID of icon            ; -1 = without icon

        ASCIIZ   TypeOfCartridge             ; "Tour guide", "Wherigo cache", etc.
        ASCIIZ   Player                      ; Name of player downloaded cartridge
        LONG     PlayerID                    ; ID of player in the Groundspeak database

        ASCIIZ   CartridgeName               ; "Name of this cartridge"
        ASCIIZ   CartridgeGUID
        ASCIIZ   CartridgeDescription        ; "This is a sample cartridge"
        ASCIIZ   StartingLocationDescription ; "Nice parking"
        ASCIIZ   Version                     ; "1.2"
        ASCIIZ   Author                      ; Author of cartridge
        ASCIIZ   Company                     ; Company of cartridge author
        ASCIIZ   RecommendedDevice           ; "Garmin Colorado", "Windows PPC", etc.

        INT      Length                      ; Length of CompletionCode
        ASCIIZ   CompletionCode              ; Normally 15/16 characters

    @address_of_FIRST_object (with ObjectID = 0):
        ; always Lua bytecode
        INT      Length                      ; Length of Lua bytecode
        BYTE[Length]    ContentOfObject      ; Lua bytecode

    @address_of_ALL_OTHER_objects (with ID > 0):
        BYTE     ValidObject
        if (ValidObject == 0)
        {
            ; when ValidObject == 0, it means that object is DELETED and does
            ; not exist in cartridge. Nothing else follows.
        }
        else
        {
            ; Object type: 1=bmp, 2=png, 3=jpg, 4=gif, 17=wav, 18=mp3, 19=fdl, 
            ; 20=snd, 21=ogg, 33=swf, 49=txt, other values have unknown meaning
            INT           ObjectType               
            INT           Length
            BYTE[Length]  content_of_object
        }

    @end

    Varibles

        BYTE   = unsigned char (1 byte)
        SHORT  = signed short (2 bytes, little endian)
        USHORT = unsigned short (2 bytes, little endian)
        INT    = signed long (4 bytes, little endian)
        UINT   = unsigned long (4 bytes, little endian)
        LONG   = signed long (8 bytes, little endian)
        DOUBLE = double-precision floating point number (8 bytes)
        ASCIIZ = zero-terminated string ("hello world!", 0x00)