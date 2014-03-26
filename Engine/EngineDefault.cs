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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.RegularExpressions;

namespace WF.Compiler
{
	public class EngineDefault : IEngine
	{
		DeviceType _device = DeviceType.Emulator;
		List<MediaFormat> _mediaFormats = new List<MediaFormat>();

		readonly string _luaCodeExtBegin = @"";
		readonly string _luaCodeExtEnd = @"";
		readonly string _luaCodeExtOnStart = @"";

		public EngineDefault (DeviceType device, List<MediaFormat> mediaFormats)
		{
			_device = device;
			_mediaFormats = mediaFormats;
		}

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
		public Cartridge ConvertCartridge(Cartridge cartridge)
		{
			foreach(Media m in cartridge.Medias)
				m.Resource = ConvertMedia(m);

			string luaCode = cartridge.LuaCode;

			cartridge.LuaCode = ConvertCode(luaCode, cartridge.Variable);

			return cartridge;
		}

		/// <summary>
		/// Converts a string into GWC header file format.
		/// </summary>
		/// <returns>String in GWC header file format.</returns>
		/// <param name="text">Original string.</param>
		public string ConvertString(string text)
		{
			return text;
		}

		/// <summary>
		/// Converts the Lua code into the correct Garmin dependent code.
		/// </summary>
		/// <remarks>
		/// Insert any special code to correct bugs for Garmins.
		/// </remarks>
		/// <returns>String of Lua code in garmin dependent format.</returns>
		/// <param name="code">Lua code.</param>
		string ConvertCode(string luaCode, string variable)
		{
			// Workaround for problems with not handled breaks of inputs
			luaCode = Regex.Replace(luaCode, @":OnGetInput\(input\)", ":OnGetInput(input)" + Environment.NewLine + "if input == nil then return end");

			if (!String.IsNullOrEmpty(_luaCodeExtBegin))
				luaCode = Regex.Replace(luaCode, @"require\s*\(?[""']Wherigo[""']\)?", "require \"Wherigo\"" + Environment.NewLine + _luaCodeExtBegin);
			if (!String.IsNullOrEmpty(_luaCodeExtEnd))
				luaCode = Regex.Replace(luaCode, @"return\s*" + variable, _luaCodeExtEnd + Environment.NewLine + "return " + variable);
			if (!String.IsNullOrEmpty(_luaCodeExtOnStart))
				luaCode = Regex.Replace(luaCode, variable + @":OnStart\(\)", variable + ":OnStart()" + Environment.NewLine + _luaCodeExtOnStart);

			return luaCode;
		}

		/// <summary>
		/// Converts the media in a valid format for this player and returns 
		/// a stream with the data.
		/// </summary>
		/// <remarks>
		/// Checks, which resource belongs to this player, change the size 
		/// or format, if needed, and 
		/// creates a memory stream with the resulting data.
		/// </remarks>
		/// <returns>Stream with The media.</returns>
		/// <param name="media">Media.</param>
		MediaResource ConvertMedia(Media media)
		{
			MediaResource res;

			// Are there any resources
			if (media.Resources.Count < 1)
				return null;

			res = media.Resources[0];

			// Get the last good media resource that could be found. 
			// This means, if a filename contains the device type or the directives list contains the device type. 
			foreach(MediaResource mr in media.Resources) {
				if (_mediaFormats.Contains(mr.Type) && mr.Type.IsImage() == media.Resources[0].Type.IsImage() && 
					(mr.Directives.Contains(_device.ToString().ToLower()) || mr.Filename.ToLower().Contains(_device.ToString().ToLower())))
						res = mr;
			}

			if (res == null)
				return null;

			// Create MemoryStream
			MediaResource result = new MediaResource();

			result.Filename = res.Filename;
			result.Directives = res.Directives;
			result.Data = res.Data;

			// Now remove all resources, because we don't need them anymore
			media.Resources = null;

			return result;
		}

		#region Graphics

		#endregion

		#region Builder Functions

		/// <summary>
		/// Determines whether this Lua code is from Urwigo.
		/// </summary>
		/// <returns><c>true</c> if this Lua code is from Urwigo; otherwise, <c>false</c>.</returns>
		/// <param name="lua">Lua code.</param>
		public static bool IsUrwigo(string lua)
		{
			return lua.Contains ("Urwigo") && lua.Contains ("dtable");
		}

		/// <summary>
		/// Determines whether this Lua code is from Earwigo.
		/// </summary>
		/// <returns><c>true</c> if this Lua code is from Earwigo; otherwise, <c>false</c>.</returns>
		/// <param name="lua">Lua code.</param>
		public static bool IsEarwigo(string lua)
		{
			return lua.Contains ("WWB_deobf");
		}

		#endregion
	}
}

