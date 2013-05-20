///
/// WF.Compiler - A Wherigo Compiler.
/// Copyright (C) 2012-2013  Dirk Weltz <web@weltz-online.de>
///
/// This program is free software: you can redistribute it and/or modify
/// it under the terms of the GNU Lesser General Public License as
/// published by the Free Software Foundation, either version 3 of the
/// License, or (at your option) any later version.
/// 
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
/// GNU Lesser General Public License for more details.
/// 
/// You should have received a copy of the GNU Lesser General Public License
/// along with this program.  If not, see <http://www.gnu.org/licenses/>.
/// 

using System;
using System.IO;
using Ionic.Zip;
using SharpLua;

namespace WF.Compiler
{
    public class Compiler
    {
        public static void Main(string[] args)
        {
            Compiler compiler = new Compiler();
            MemoryStream ms = compiler.Compile(@"C:\Test.gwz", DeviceType.Garmin, "User", 0, "CompletionCode");
            if (ms == null)
                throw new System.InvalidOperationException("GWZ file could not be compiled");
            // Now we have the gwc file in memory, so we could save it to where we want
            FileStream fs = new FileStream(@"C:\Temp\Test.gwc", FileMode.Create);
            ms.Position = 0;
            fs.Write(ms.GetBuffer(), 0, (int)ms.Length);
            fs.Close();
        }

        /// <summary>
        /// Compile the file filename for device and place user and completionCode into the resulting gwc file
        /// </summary>
        /// <param name="filename">Filename of the gwz file with path.</param>
        /// <param name="device">Name of the device for which the resulting gwc is compiled.</param>
        /// <param name="username">Username, which is wrote to gwc file and later used as Player.Name.</param>
        /// <param name="userid">User ID from Groundspeak, which is wrote to gwc file.</param>
        /// <param name="completionCode">CompletionCode, which is wrote to gwc file and later used as Player.CompletionCode.</param>
        /// <returns>Memory stream with the gwc file.</returns>
        public MemoryStream Compile(string filename, DeviceType device, string username = "", long userid = 0, string completionCode = "")
        {
            // Create object for player specific code
            Player player = new Player(device);
            // Create parser with filename
            Parser parser = new Parser(filename);
            // Open gwz for later use
            ZipFile zip = new ZipFile(parser.FilenameGwz);
            // Create object for all data belonging to the cartridge
            Cartridge cartridge = new Cartridge ();

            // Check, if the gwz file has the right format
            if (!parser.CheckGWZ(zip, cartridge))
            {
                // Error in parsing gwz file
                throw new InvalidOperationException(String.Format("Line {0}, Column {1}: {2}", parser.Errors[0].Line, parser.Errors[0].Column, parser.Errors[0].Message));
            }

            // Create LuaInterface
            LuaInterface lua = new LuaInterface();
            string luaCode;

            // Create new Lua file with player specific characters and all other stuff
            luaCode = parser.UpdateLua(zip,player);

            // Only for debug reasons
            // saveLuaCode(luaCode);

            // Now we have a string with the Lua code, which have the right coding
            // So go on and compile the Lua code

            // LoadString of Lua code = compile Lua code
            LuaFunction func = lua.LoadString(luaCode, cartridge.Name);

            // Dump Lua code to memory stream, so we could later save it to gwc file
            MemoryStream luaBinary = new MemoryStream();
            dumpCode(lua.LuaState,luaBinary);

            // Create memory stream to write gwc file
            MemoryStream stream = new MemoryStream();
            BinaryWriter output = new BinaryWriter(stream);
            
            long savePosition;

            // Write gwc signature
            output.Write(new byte[7] { 0x02, 0x0a, 0x43, 0x41, 0x52, 0x54, 0x00 });

            // Write number of images
            output.Write(getShort((short)(cartridge.MediaList.Count+1)));

            // Write media table
            for (int i = 0; i <= cartridge.MediaList.Count; i++)
            {
                output.Write(getShort((short)i));
                output.Write(getInt((int)0));
            }

            // Write size of header
            output.Write(getInt(0));

            // Save position, where header starts
            long startHeader = output.BaseStream.Position;

            // Write header

            // Write position
            output.Write(getDouble((float) cartridge.Latitude));
            output.Write(getDouble((float) cartridge.Longitude));
            output.Write(getDouble((float) cartridge.Altitude));
            // Write creation date
            // TODO: Replace with creation date
            string date = cartridge.CreateDate;
            output.Write(getLong(0));
            // Write media index
            short splash = -1;
            for (int i = 0; i < cartridge.MediaList.Count; i++)
                if (cartridge.Splash != null && cartridge.Splash.Equals(cartridge.MediaList[i].Variable))
                    splash = (short)(i + 1);
            output.Write(getShort(splash));
            // Write icon index
            short icon = -1;
            for (int i = 0; i < cartridge.MediaList.Count; i++)
                if (cartridge.Icon != null && cartridge.Icon.Equals(cartridge.MediaList[i].Variable))
                    icon = (short)(i + 1);
            output.Write(getShort(icon));
            // Write cartridge type
            output.Write(getAscii(player.ConvertGWCString(cartridge.Activity)));
            // Write player data
            output.Write(getAscii(player.ConvertGWCString(username)));
            output.Write(getLong(userid));
            // Write cartridge relevant information
            output.Write(getAscii(player.ConvertGWCString(cartridge.Name)));
            output.Write(getAscii(cartridge.Id));
            output.Write(getAscii(player.ConvertGWCString(cartridge.Description)));
            output.Write(getAscii(player.ConvertGWCString(cartridge.StartingLocationDescription)));
            output.Write(getAscii(cartridge.Version));
            output.Write(getAscii(player.ConvertGWCString(cartridge.Author)));
            output.Write(getAscii(player.ConvertGWCString(cartridge.Company)));
            output.Write(getAscii(player.ConvertGWCString(cartridge.TargetDevice)));
            // Write CompletionCode length
            output.Write(getInt(completionCode.Length+1));
            // Write CompletionCode
            output.Write(getAscii(completionCode));

            // Save position for later writing
            savePosition = output.BaseStream.Position;
            // Goto header length position and save the length
            output.BaseStream.Position = startHeader - 4;
            output.Write(getInt((int) (savePosition - startHeader)));
            output.BaseStream.Position = savePosition;

            // Write Lua binary code
            writeToMediaTable(output,0);
            output.Write(getInt((int)luaBinary.Length));
            output.Write(luaBinary.ToArray());

            // Now save all media files
            for (short i = 0; i < cartridge.MediaList.Count; i++)
            {
                // Write position for media table
                writeToMediaTable(output, (short)(i+1));
                // Check media for right entry
                cartridge.MediaList[i].Entry = player.CheckMedia(cartridge.MediaList[i]);
                // Save media
                if (cartridge.MediaList[i].Entry == -1)
                {
                    // No valid media file is found in resource list
                    output.Write((byte) 0);
                }
                else
                {
                    // Valid file found, so save type, length and bytes
                    output.Write((byte) 1);
                    output.Write(getInt(cartridge.MediaList[i].MediaType));
                    byte[] media = cartridge.MediaList[i].GetMediaAsByteArray(zip);
                    output.Write(getInt(media.Length));
                    output.Write(media);
                    }
            }

            return stream;
        }

        /// <summary>
        /// Save actuall position to media table.
        /// </summary>
        /// <param name="output">BinaryWrite to write to.</param>
        /// <param name="position">Position of the media, which should be updated (Lua binary is ever 0).</param>
        private void writeToMediaTable ( BinaryWriter output, short position )
        {
            // Save active position
            long savePosition = output.BaseStream.Position;

            // Jump to media table, save active position and jump back
            output.BaseStream.Position = 9 + position * 6;
            output.Write(getShort(position));
            output.Write(getInt((int) savePosition));
            output.BaseStream.Position = savePosition;

            return;
        }

        /// <summary>
        /// Get value as byte array in little endian order.
        /// </summary>
        /// <param name="value">Short to convert to byte array (2 byte).</param>
        /// <returns>Byte array with 2 bytes.</returns>
        private byte[] getShort(Int16 value)
        {
            byte[] result = new byte[2] { 0, 0 };

            result = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        /// <summary>
        /// Get value as byte array in little endian order.
        /// </summary>
        /// <param name="value">Int to convert to byte array (4 byte).</param>
        /// <returns>Byte array with 4 bytes.</returns>
        private byte[] getInt(Int32 value)
        {
            byte[] result = new byte[4] { 0, 0, 0, 0 };

            result = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        /// <summary>
        /// Get value as byte array in little endian order.
        /// </summary>
        /// <param name="value">Long to convert to byte array (8 byte).</param>
        /// <returns>Byte array with 8 bytes.</returns>
        private byte[] getLong(Int64 value)
        {
            byte[] result = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

            result = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        /// <summary>
        /// Get value as byte array in little endian order.
        /// </summary>
        /// <param name="value">Double to convert to byte array (8 byte).</param>
        /// <returns>Byte array with 8 bytes.</returns>
        private byte[] getDouble(double value)
        {
            byte[] result = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

            result = BitConverter.GetBytes((double)value);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        /// <summary>
        /// Get string value as byte array with a zero as end.
        /// </summary>
        /// <param name="value">String to convert to a null terminated byte array.</param>
        /// <returns>Byte array with string.length+1 bytes.</returns>
        private byte[] getAscii(string value)
        {
            byte[] result = new byte[value.Length + 1];

            for (int i = 0; i < value.Length; i++)
                result[i] = (byte)(value[i] & 0xff);

            result[value.Length] = (byte)0;

            return result;
        }

        /// <summary>
        /// Write the compiled Lua binary to the memory stream output.
        /// </summary>
        /// <param name="L">LuaState of the functions, which should be written.</param>
        /// <param name="output">Memory stream which the binary code should be written to.</param>
        /// <returns>Status of the operation (0 = ok). </returns>
        private int dumpCode(Lua.LuaState L, MemoryStream output)
        {
            int status;

            Lua.lua_lock(L);
            Lua.Proto f = Lua.clvalue(L.top).l.p;
            Lua.api_checknelems(L, 1);
            Lua.lua_TValue o = L.top - 1;
            status = Lua.luaU_dump(L, f, dumpWriter, output, 0);
            Lua.lua_unlock(L);

            return status;
        }

        /// <summary>
        /// Did the write operation to the stream
        /// </summary>
        /// <param name="L">LuaState, which should be written.</param>
        /// <param name="p">CharPtr to char array with data. Only the byte is used.</param>
        /// <param name="sz">Size of the array.</param>
        /// <param name="ud">Memory stream, which is used.</param>
        /// <returns>Zero if all right.</returns>
        private int dumpWriter(Lua.LuaState L, Lua.CharPtr p, uint sz, object ud)
        {
            int result = 0;
            MemoryStream ms = (MemoryStream)ud;

            foreach (char c in p.chars)
                ms.WriteByte((byte)c);

            return result;
        }

        // TODO: Remove
        // Only for degug reasons
        private void saveLuaCode(string text)
        {
            FileStream fs = new FileStream(@"s:\entwicklung\csharp\debug.lua",FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(text);
            bw.Close();
        }

    }

}

