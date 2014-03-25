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
	public interface IEngine
	{
		/// <summary>
		/// Converts the Cartridge object in a format valid for the given engine.
		/// </summary>
		/// <remarks>
		/// - Convert Lua code
		/// - Convert strings in special format and insert any special code
		/// - Convert medias
		/// </remarks>
		/// <returns>Cartridge object in for this engine correct format.</returns>
		/// <param name="cartridge">Cartridge object to convert.</param>
		Cartridge ConvertCartridge(Cartridge cartridge);

		/// <summary>
		/// Converts a string into GWC header file format.
		/// </summary>
		/// <returns>String in GWC header file format.</returns>
		/// <param name="text">Original string.</param>
		string ConvertString(string text);
	}
}

