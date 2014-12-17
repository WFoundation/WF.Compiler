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
using Ionic.Zip;
using System.Collections.Generic;

namespace WF.Compiler
{
	public class GWZ : IInput
	{
		int _luaFiles = 0;
		Stream _stream;
		ZipFile _zip;
		ZipEntry _luaFile = null;
		Cartridge cartridge = null;

		public GWZ (Stream stream)
		{
			_stream = stream;
		}

		public void Check()
		{
			// Is there a valid input stream
			if (_stream == null)
				throw new FileNotFoundException("No valid file");

			// Now read gwz file and save for later use
			_zip = ZipFile.Read(_stream);

			if (_zip == null)
				throw new FileLoadException("No valid gwz file");

			foreach(ZipEntry zipEntry in _zip.Entries)
			{
				switch(Path.GetExtension(zipEntry.FileName).ToLower())
				{
					case ".lua":
						_luaFile = zipEntry;
						_luaFiles += 1;
						break;
				}
			}

			// Is there a Lua file?
			if (_luaFile == null)
				throw new FileNotFoundException("No valid Lua file found");

			// Is there more than one Lua file
			if (_luaFiles > 1)
				throw new FileLoadException("More than one Lua file found");

			// Any compilation errors of the Lua file
			LUA.Check(_zip[_luaFile.FileName].OpenReader(), _luaFile.FileName);

			// Extract cartridge data from Lua file
			cartridge = LUA.Extract(_zip[_luaFile.FileName].OpenReader());

			// Save Lua file name for later use
			cartridge.LuaFileName = _luaFile.FileName;

			// Now check, if all media resources files exist
			foreach(Media media in cartridge.Medias) {
				foreach(MediaResource resource in media.Resources) {
					// Check, if filename is in list of files
					if (!_zip.EntryFileNames.Contains(resource.Filename))
					{
						if (string.IsNullOrWhiteSpace(resource.Filename))
							throw new FileNotFoundException("The Lua file is referencing a file without a filename");
						else
							throw new FileNotFoundException(String.Format("The GWZ is missing a file referred to by the cartridge's code. The file name is: {0}", resource.Filename));
					}
				}
			}

			// Now all is checked without any problems
			// So it seams, that this GWZ file is valid
		}

		public Cartridge Load()
		{
			// Is there a valid gwz file
			if (_zip == null)
				return null;

			// Is there a valid Lua file
			if (_luaFile == null)
				return null;

			if (cartridge == null)
				// Extract cartridge data from Lua file
				cartridge = LUA.Extract(_zip[_luaFile.FileName].OpenReader());

			// Retrive input streams for medias
			foreach(Media media in cartridge.Medias) {
				foreach(MediaResource r in media.Resources) {
					// Load data of file into byte array
					var br = new BinaryReader(_zip[r.Filename].OpenReader());
					r.Data = new byte[br.BaseStream.Length];
					r.Data = br.ReadBytes(r.Data.Length);
					br = null;
				}
			}

			return cartridge;
		}
	}
}

