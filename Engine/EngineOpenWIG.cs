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
	public class EngineOpenWIG : IEngine
	{
		List<MediaType> _mediaFormats = new List<MediaType>() { 
			MediaType.BMP, 
			MediaType.PNG, 
			MediaType.JPG, 
			MediaType.GIF, 
			MediaType.WAV, 
			MediaType.MP3,
			MediaType.OGG
		};

		public EngineOpenWIG ()
		{
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
		/// Converts the media in a valid format for this player and returns 
		/// a valid resource, if there are any.
		/// </summary>
		/// <remarks>
		/// Checks, which resource belongs to this player, change the size 
		/// or format, if needed, and creates a MediaResource.
		/// </remarks>
		/// <returns>MediaResource with the correct media or null.</returns>
		/// <param name="media">Media.</param>
		MediaResource ConvertMedia(Media media)
		{
			MediaResource res = null;

			// Are there any resources
			if (media.Resources.Count < 1)
				return null;

			// Get the last good media resource that could be found
			foreach(MediaResource mr in media.Resources) {
				if (_mediaFormats.Contains(mr.Type) && mr.Type.IsImage() == media.Resources[0].Type.IsImage() && (mr == media.Resources[0] || mr.Directives.Contains("openwig") || mr.Filename.ToLower().Contains("openwig")))
					res = mr;
				if (_mediaFormats.Contains(mr.Type) && mr.Type.IsSound() == media.Resources[0].Type.IsSound() && (mr == media.Resources[0] || mr.Directives.Contains("openwig") || mr.Filename.ToLower().Contains("openwig")))
					res = mr;
			}

			if (res == null)
				return null;

			// Create MemoryStream
			MediaResource result = new MediaResource();

			result.Filename = res.Filename;
			result.Directives = res.Directives;
			result.Type = res.Type;
			result.Data = res.Data;

			// Now remove all resources, because we don't need them anymore
			media.Resources = null;

			return result;
		}
	}
}