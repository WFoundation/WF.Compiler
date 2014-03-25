///
/// WF.Compiler - A Wherigo Compiler, which use the Wherigo Foundation Core.
/// Copyright (C) 2012-2014  Dirk Weltz <mail@wfplayer.com>
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

namespace WF.Compiler
{
	public class GWC : IOutput
	{
		public MemoryStream Create (Cartridge cartridge, string username, long userId, string completionCode)
		{
			// Create memory stream to write gwc file
			MemoryStream stream = new MemoryStream();
			BinaryWriter output = new BinaryWriter(stream);

			long savePosition;

			// Write gwc signature
			output.Write(new byte[7] { 0x02, 0x0a, 0x43, 0x41, 0x52, 0x54, 0x00 });

			// Write number of images
			output.Write(GetShort((short)(cartridge.Medias.Count+1)));

			// Write media table
			for (int i = 0; i <= cartridge.Medias.Count; i++)
			{
				output.Write(GetShort((short)i));
				output.Write(GetInt((int)0));
			}

			// Write size of header
			output.Write(GetInt(0));

			// Save position, where header starts
			long startHeader = output.BaseStream.Position;

			// Write header

			// Write position
			output.Write(GetDouble((float) cartridge.Latitude));
			output.Write(GetDouble((float) cartridge.Longitude));
			output.Write(GetDouble((float) cartridge.Altitude));
			// Write creation date
			// TODO: Replace with creation date
			string date = cartridge.CreateDate;
			output.Write(GetLong(0));
			// Write poster and icon index
			output.Write(GetShort((short)cartridge.Poster));
			output.Write(GetShort((short)cartridge.Icon));
			// Write cartridge type
			output.Write(GetAscii(cartridge.Activity));
			// Write player data
			output.Write(GetAscii(username));
			output.Write(GetLong(userId));
			// Write cartridge relevant information
			output.Write(GetAscii(cartridge.Name));
			output.Write(GetAscii(cartridge.Id));
			output.Write(GetAscii(cartridge.Description));
			output.Write(GetAscii(cartridge.StartingLocationDescription));
			output.Write(GetAscii(cartridge.Version));
			output.Write(GetAscii(cartridge.Author));
			output.Write(GetAscii(cartridge.Company));
			output.Write(GetAscii(cartridge.TargetDevice));
			// Write CompletionCode length
			output.Write(GetInt(completionCode.Length + 1));
			// Write CompletionCode
			output.Write(GetAscii(completionCode));

			// Save position for later writing
			savePosition = output.BaseStream.Position;
			// Goto header length position and save the length
			output.BaseStream.Position = startHeader - 4;
			output.Write(GetInt((int) (savePosition - startHeader)));
			output.BaseStream.Position = savePosition;

			// Write Lua binary code
			WriteToMediaTable(output, 0);
			output.Write(GetInt((int)cartridge.Chunk.Length));
			output.Write(cartridge.Chunk);

			// Now save all media files
			for (short i = 0; i < cartridge.Medias.Count; i++)
			{
				// Write position for media table
				WriteToMediaTable(output, (short)(i + 1));
				// Check media for right entry
				MediaResource mr = cartridge.Medias[i].Resource;
				// Save media
				if (mr == null)
				{
					// No valid media file is found in resource list
					output.Write((byte) 0);
				}
				else
				{
					// Valid file found, so save type, length and bytes
					output.Write((byte) 1);
					output.Write(GetInt((int)mr.Type));
					output.Write(GetInt((int)mr.Data.Length));
					output.Write(mr.Data);
				}
			}

			// Go to begining of memory stream
			stream.Position = 0;

			return stream;
		}

		#region Helper Functions

		/// <summary>
		/// Save actual position to media table.
		/// </summary>
		/// <param name="output">BinaryWrite to write to.</param>
		/// <param name="position">Position of the media, which should be updated (Lua binary is ever 0).</param>
		void WriteToMediaTable ( BinaryWriter output, short position )
		{
			// Save active position
			long savePosition = output.BaseStream.Position;

			// Jump to media table, save active position and jump back
			output.BaseStream.Position = 9 + position * 6;
			output.Write(GetShort(position));
			output.Write(GetInt((int) savePosition));
			output.BaseStream.Position = savePosition;

			return;
		}

		#endregion

		#region Conversion

		/// <summary>
		/// Get value as byte array in little endian order.
		/// </summary>
		/// <param name="value">Short to convert to byte array (2 byte).</param>
		/// <returns>Byte array with 2 bytes.</returns>
		private byte[] GetShort(Int16 value)
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
		private byte[] GetInt(Int32 value)
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
		private byte[] GetLong(Int64 value)
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
		private byte[] GetDouble(double value)
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
		private byte[] GetAscii(string value)
		{
			byte[] result = new byte[value.Length + 1];

			for (int i = 0; i < value.Length; i++)
				result[i] = (byte)(value[i] & 0xff);

			result[value.Length] = (byte)0;

			return result;
		}

		#endregion

		// TODO: Remove
		// Only for debug reasons
		private void saveLuaCode(string text)
		{
			FileStream fs = new FileStream(@"s:\entwicklung\csharp\debug.lua",FileMode.Create);
			BinaryWriter bw = new BinaryWriter(fs);
			bw.Write(text);
			bw.Close();
		}

	}
}

