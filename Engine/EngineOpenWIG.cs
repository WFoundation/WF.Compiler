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
		readonly string _luaCodeExt = @"require ""Wherigo""

WFCompShowScreen = Wherigo.ShowScreen
Wherigo.ShowScreen = function (arg1,arg2) pcall(WFCompShowScreen(arg1,arg2)) end" + Environment.NewLine;

		List<MediaFormat> _mediaFormats = new List<MediaFormat>() { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };

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
			cartridge.Activity = ConvertString(cartridge.Activity);
			cartridge.Name = ConvertString(cartridge.Name);
			cartridge.Description = ConvertString(cartridge.Description);
			cartridge.StartingLocationDescription = ConvertString(cartridge.StartingLocationDescription);
			cartridge.Author = ConvertString(cartridge.Author);
			cartridge.Company = ConvertString(cartridge.Company);
			cartridge.TargetDevice = ConvertString(cartridge.TargetDevice);

			foreach(Media m in cartridge.Medias)
				m.Resource = ConvertMedia(m);

			string luaCode = cartridge.LuaCode;

			// Now make special operations with the source code for different builders

			cartridge.LuaCode = ConvertCode(luaCode);

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
		/// Converts the Lua code into the correct OpenWIG dependent code.
		/// </summary>
		/// <remarks>
		/// Insert any special code to correct bugs for OpenWIG.
		/// </remarks>
		/// <returns>String of Lua code in garmin dependent format.</returns>
		/// <param name="code">Lua code.</param>
		string ConvertCode(string luaCode)
		{
			// Workaround for problems with not handled breaks of inputs
			luaCode = luaCode.Replace(":OnGetInput(input)", ":OnGetInput(input)" + Environment.NewLine + "if input == nil then return end");

			// Add special code
			string require = null;

			if (luaCode.Contains("require \"Wherigo\""))
				require = "require \"Wherigo\"";
			if (luaCode.Contains("require (\"Wherigo\")"))
				require = "require (\"Wherigo\")";
			if (luaCode.Contains("require(\"Wherigo\")"))
				require = "require(\"Wherigo\")";

			if (require != null)
				luaCode = luaCode.Replace(require, _luaCodeExt);

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

			// Is there a jpeg encoder?
			// Jpeg image codec
			ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

			if (jpegCodec == null)
				throw new InvalidOperationException("No JPEG encoder found");

			// Are there any resources
			if (media.Resources.Count < 1)
				return null;

			res = media.Resources[0];

			// Get the last good media resource that could be found
			foreach(MediaResource mr in media.Resources) {
				if (_mediaFormats.Contains(mr.Type) && mr.Type.IsImage() == media.Resources[0].Type.IsImage() && (mr.Directives.Contains("openwig") || mr.Filename.ToLower().Contains("openwig")))
					res = mr;
			}

			if (res == null)
				return null;

			// Create MemoryStream
			MediaResource result = new MediaResource();

			result.Filename = res.Filename;
			result.Directives = res.Directives;

			EncoderParameters encParams = new EncoderParameters(2);
			encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 24L);
			encParams.Param[1] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 70L);

			if (res.Type.IsImage()) {
				// Image
				Image img;
				using(MemoryStream ims = new MemoryStream(res.Data)) {
					img = Image.FromStream(ims);
					// Do special things with the image (resize, bit depth, ...)
					if (!res.Directives.Contains("noresize") && img.Width > 230)
						img = ResizeImage(img, 230);
					// OpenWIG can handle different formats, but now convert it to jpg
					using(MemoryStream oms = new MemoryStream()) {
						img.Save(oms, jpegCodec, encParams);
						result.Data = oms.ToArray();
					}
				}
				result.Type = MediaFormat.jpg;
			} else {
				// Sound
				if (res.Type == MediaFormat.fdl) {
					result.Type = MediaFormat.fdl;
					result.Data = res.Data;
				} else {
					result = null;
				}
			}

			// Now remove all resources, because we don't need them anymore
			media.Resources = null;

			return result;
		}

		#region Graphics

		// Found at: http://tech.pro/tutorial/620/csharp-tutorial-image-editing-saving-cropping-and-resizing

		private ImageCodecInfo GetEncoderInfo(string mimeType)
		{
			// Get image codecs for all image formats
			ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

			// Find the correct image codec
			for (int i = 0; i < codecs.Length; i++)
				if (codecs[i].MimeType == mimeType)
					return codecs[i];
			return null;
		}

		private static Image ResizeImage(Image imgToResize, int width)
		{
			int sourceWidth = imgToResize.Width;
			int sourceHeight = imgToResize.Height;

			float nPercent = 0;

			nPercent = ((float)width / (float)sourceWidth);

			int destWidth = (int)(sourceWidth * nPercent);
			int destHeight = (int)(sourceHeight * nPercent);

			Bitmap b = new Bitmap(destWidth, destHeight, PixelFormat.Format24bppRgb);
			Graphics g = Graphics.FromImage((Image)b);
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;

			g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
			g.Dispose();

			return (Image)b;
		}

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

