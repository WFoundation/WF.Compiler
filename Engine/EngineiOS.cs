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
	public class EngineiOS : IEngine
	{
		string _mediaSelector;
		int _maxImageWidth;

		List<MediaType> _mediaFormats = new List<MediaType>() { 
			MediaType.BMP, 
			MediaType.PNG, 
			MediaType.JPG, 
			MediaType.GIF, 
			MediaType.WAV, 
			MediaType.MP3 
		};

		public EngineiOS (DeviceType device)
		{
			switch (device) {
			case DeviceType.iPhoneRetina:
				_mediaSelector = "iphoneretina";
				_maxImageWidth = 640;
				break;
			case DeviceType.iPad:
				_mediaSelector = "ipad";
				_maxImageWidth = 768;
				break;
			case DeviceType.iPadRetina:
				_mediaSelector = "ipadretina";
				_maxImageWidth = 1536;
				break;
			default:
				_mediaSelector = "iphone";
				_maxImageWidth = 320;
				break;
			}
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

			// Is there a jpeg encoder?
			// Jpeg image codec
			ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

			if (jpegCodec == null)
				throw new InvalidOperationException("No JPEG encoder found");

			// Are there any resources
			if (media.Resources.Count < 1)
				return null;

			// Get the last good media resource that could be found
			foreach(MediaResource mr in media.Resources) {
				if (_mediaFormats.Contains(mr.Type) && mr.Type.IsImage() == media.Resources[0].Type.IsImage() && (mr == media.Resources[0] || mr.Directives.Contains(_mediaSelector) || mr.Filename.ToLower().Contains(_mediaSelector)))
					res = mr;
				if (_mediaFormats.Contains(mr.Type) && mr.Type.IsSound() == media.Resources[0].Type.IsSound() && (mr == media.Resources[0] || mr.Directives.Contains(_mediaSelector) || mr.Filename.ToLower().Contains(_mediaSelector)))
					res = mr;
			}

			if (res == null)
				return null;

			// Create MediaResurce
			MediaResource result = new MediaResource();

			result.Filename = res.Filename;
			result.Directives = res.Directives;

			EncoderParameters encParams = new EncoderParameters(2);
			encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 24L);
			encParams.Param[1] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);

			if (res.Type.IsImage()) {
				// Image
				Image img;
				using(MemoryStream ims = new MemoryStream(res.Data)) {
					img = Image.FromStream(ims);
					// Do special things with the image (resize, bit depth, ...)
					if (!res.Directives.Contains("noresize") && img.Width > _maxImageWidth)
						img = ResizeImage(img, _maxImageWidth);
					// Garmin can only handle jpg
					using(MemoryStream oms = new MemoryStream()) {
						img.Save(oms, jpegCodec, encParams);
						result.Data = oms.ToArray();
					}
				}
				result.Type = MediaType.JPG;
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
	}
}