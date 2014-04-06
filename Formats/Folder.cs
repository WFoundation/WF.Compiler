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
	public class Folder : IInput
	{
		Stream _stream;
		string _luaFileName;
		Cartridge cartridge = null;

		public Folder (Stream stream, string luaFileName)
		{
			_stream = stream;
			_luaFileName = luaFileName;
		}

		public void Check()
		{
			// Is there a valid input stream
			if (_stream == null)
				throw new FileNotFoundException("No valid file");

			// Any compilation errors of the Lua file
			_stream.Position = 0;
			LUA.Check(_stream);

			// Extract cartridge data from Lua file
			_stream.Position = 0;
			cartridge = LUA.Extract(_stream);

			// Save Lua file name for later use
			cartridge.LuaFileName = _luaFileName;

			// All media files should be in the same directory as the Lua file
			string path = Path.GetDirectoryName(_luaFileName);

			// Now check, if all media resources files exist
			foreach(Media media in cartridge.Medias) {
				foreach(MediaResource resource in media.Resources) {
					// Check, if filename is in list of files
					if (!File.Exists(Path.Combine(path, resource.Filename)))
						throw new FileNotFoundException("Folder don't contain file", resource.Filename);
				}
			}

			// Now all is checked without any problems
			// So it seams, that this folder is valid
		}

		public Cartridge Load()
		{
			if (cartridge == null)
				// Extract cartridge data from Lua file
				cartridge = LUA.Extract(_stream);

			// All media files should be in the same directory as the Lua file
			string path = Path.GetDirectoryName(_luaFileName);

			// Retrive input streams for medias
			foreach(Media media in cartridge.Medias) {
				foreach(MediaResource r in media.Resources) {
					// Load data of file into byte array
					var br = new BinaryReader(new FileStream(Path.Combine(path, r.Filename), FileMode.Open));
					r.Data = new byte[br.BaseStream.Length];
					r.Data = br.ReadBytes(r.Data.Length);
					br = null;
				}
			}

			return cartridge;
		}
	}
}