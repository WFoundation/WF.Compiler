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
	public class EngineGarmin : IEngine
	{
		readonly Encoding _encodingWin1252 = Encoding.GetEncoding(1252);
		readonly ImageCodecInfo _jpegCodec;
		readonly EncoderParameters _encParams = new EncoderParameters(2);

		// Put all in the beginning in one line, so that crash report lines are correct
		// Remove of Garmin crash with cancelled inputs is not possible
		readonly string _luaCodeExt = @"require ""Wherigo"" function _main () {0}
end
-- Insert workaround code before cartridge run
-- Standard newline for Garmins
Env.NewLine = ""<BR>\n""
-- Remove Garmin crash of ShowScreen
WFCompShowScreen = Wherigo.ShowScreen
Wherigo.ShowScreen = function (arg1,arg2) pcall(WFCompShowScreen, arg1, arg2) end
-- Remove Garmin crash with input returning nil
function Wherigo.ZInput.GetInput(self, input)
  local inputString = input or ""<cancelled>""
  Wherigo.LogMessage(""ZInput:GetInput - "" .. self.Name .. "" -> "" .. inputString)
  if type(self[""OnGetInput""]) == ""function"" and input ~= nil then
    pcall(self[""OnGetInput""], self, input)
  end
end
-- Remove Garmin bug with timer stop in OnTick
function Wherigo.ZTimer.Tick(self)
  if self.Running then
    self.Stopped = false
    if self.Type ~= ""Interval"" then
      self.Running = false
    end
    if type(self[""OnTick""]) == ""function"" then
      self[""OnTick""](self)
    end
    if self.Type == ""Interval"" and self.Running == true then
      self:begin()
    end
  end
end
-- Call original Lua code here
cartridge = _main ()
-- Insert workaround code after cartridge run
return cartridge";

		List<MediaType> _mediaFormats = new List<MediaType>() { 
			MediaType.BMP, 
			MediaType.PNG, 
			MediaType.JPG, 
			MediaType.GIF, 
			MediaType.FDL,
			MediaType.TXT
		};

		public EngineGarmin ()
		{
			_jpegCodec = GetEncoderInfo("image/jpeg");

			// Is there a jpeg encoder?
			// Jpeg image codec
			if (_jpegCodec == null)
				throw new InvalidOperationException("No JPEG encoder found");

			_encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 24L);
			_encParams.Param[1] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
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
			if (IsUrwigo(cartridge.LuaCode)) {
				luaCode = ReplaceSpecialCharacters(luaCode);
			}

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
			// Convert string from UTF-8 to Win-1252, which Garmins use
			string result = _encodingWin1252.GetString(Encoding.Convert(Encoding.UTF8, _encodingWin1252, Encoding.UTF8.GetBytes(text)));

			result = result.Replace("<BR>\n", "<BR>").Replace("\n", "<BR>");

			return result;
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
			// Replace all short strings
			luaCode = ReplaceShortStrings(luaCode);
			// Replace all long strings
			luaCode = ReplaceLongStrings(luaCode);
			// Workaround for Garmin problems
			luaCode = String.Format(_luaCodeExt, luaCode);

			return luaCode;
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
				if (_mediaFormats.Contains(mr.Type) && mr.Type.IsImage() && media.Resources[0].Type.IsImage() && (mr == media.Resources[0] || mr.Directives.Contains("garmin") || mr.Filename.ToLower().Contains("garmin")))
					res = mr;
				if (_mediaFormats.Contains(mr.Type) && mr.Type.IsSound() && media.Resources[0].Type.IsSound() && (mr == media.Resources[0] || mr.Type == MediaType.FDL || mr.Directives.Contains("garmin") || mr.Filename.ToLower().Contains("garmin")))
					res = mr;
			}

			if (res == null)
				return null;

			// Create MemoryStream
			MediaResource result = new MediaResource();

			result.Filename = res.Filename;
			result.Directives = res.Directives;

			if (res.Type.IsImage()) {
				// Image
				Image imgLoader;
				Image img;
				using(MemoryStream ims = new MemoryStream(res.Data)) {
					imgLoader = Image.FromStream(ims);
					img = new Bitmap(imgLoader);
					// Do special things with the image (resize, bit depth, ...)
					if (!res.Directives.Contains("noresize") && img.Width > 230)
						img = ResizeImage(img, 230);
					// Garmin can only handle jpg
					using(MemoryStream oms = new MemoryStream()) {
						img.Save(oms, _jpegCodec, _encParams);
						result.Data = oms.ToArray();
					}
				}
				result.Type = MediaType.JPG;
			} else {
				// Sound
				if (res.Type == MediaType.FDL) {
					result.Type = MediaType.FDL;
					result.Data = res.Data;
				} else {
					result = null;
				}
			}

			// Now remove all resources, because we don't need them anymore
			media.Resources = null;

			return result;
		}

		#region String Replacement

		string ReplaceShortStrings (string luaCode)
		{
			StringBuilder result = new StringBuilder();
			string doubleQuote = @"/->double-quote<-/";
			string singleQuote = @"/->single-quote<-/";
			string[] lines = Regex.Split(luaCode, "\r\n|\n|\r");
			Regex regex = new Regex(@"(""|')([^\1]*?)\1", RegexOptions.Multiline & RegexOptions.Compiled);

			foreach(string line in lines) {
				// We start at beginning of the line
				int startAt = 0;
				// There could be \" or \' in the string, which stops the regex. 
				// So we replace \" with doubleQuote and \' with singleQuote. Later we reverse this.
				string searchLine = line.Replace(@"\""", doubleQuote).Replace(@"\'", singleQuote);
				StringBuilder replaceLine = new StringBuilder(line.Length);
				Match match = regex.Match(searchLine);

				while(match.Success) {
					// Append all text up to the beginning "" or '
					replaceLine.Append(searchLine.Substring(startAt, match.Index - startAt));
					// Append opening " or '
					replaceLine.Append(match.Groups[1].Value);
					// Append text found in "" or ''
					replaceLine.Append(ReplaceString(match.Groups[2].Value));
					// Append closing " or '
					replaceLine.Append(match.Groups[1].Value);
					// Calc new startAt after the closing " or '
					startAt = match.Index + match.Length;
					// Search for the next match
					match = regex.Match(searchLine, startAt);
				}
				// Append the rest of this line
				replaceLine.Append(searchLine.Substring(startAt));
				// Append a newline at the end
				replaceLine.Append(Environment.NewLine);
				// Reverse any changes to \" and \'
				replaceLine.Replace(doubleQuote, @"\""").Replace(singleQuote, @"\'");
				// Add new line to result
				result.Append(replaceLine);
			}

			return result.ToString();
		}

		string ReplaceLongStrings (string luaCode)
		{
			StringBuilder result = new StringBuilder();
			Regex regex = new Regex(@"\[(=*)\[(.*?)\]\1\]", RegexOptions.Singleline); // & RegexOptions.Compiled);

			// We start at beginning of the line
			int startAt = 0;
			// There could be \" or \' in the string, which stops the regex. 
			// So we replace \" with "" and \' with ''. Later we reverse this.
			Match match = regex.Match(luaCode);

			while(match.Success) {
				// Append all text up to the beginning [[
				result.Append(luaCode.Substring(startAt, match.Index - startAt));
				// Append opening [[
				result.Append("[");
				result.Append(match.Groups[1].Value);
				result.Append("[");
				// Append text found in "" or ''
				result.Append(ReplaceString(match.Groups[2].Value));
				// Append closing ]]
				result.Append("]");
				result.Append(match.Groups[1].Value);
				result.Append("]");
				// Calc new startAt after the closing " or '
				startAt = match.Index + match.Length;
				// Search for the next match
				match = regex.Match(luaCode, startAt);
			}

			result.Append(luaCode.Substring(startAt));

			return result.ToString();
		}

		private string ReplaceString(string text)
		{
			string result = text;

			result = result.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\t", "   ");
			result = Regex.Replace(result, " {2,}", new MatchEvaluator(this.ReplaceSpaces), RegexOptions.Multiline);
			result = Regex.Replace(result, "\r\n|\n\r", "\n", RegexOptions.Multiline);
			result = Regex.Replace(result, "\r|\n", "<BR>\n", RegexOptions.Multiline);

			return result;
		}

		string ReplaceSpaces(Match m)
		{
			StringBuilder builder = new StringBuilder(" ");

			for (int i = 1; i < m.Value.Length; i++)
			{
				builder.Append("&nbsp;");
			}

			return builder.ToString();
		}

		#endregion

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
		/// Replaces escape sequences with UTF-8 (like \195) with Win-1252 codes.
		/// </summary>
		/// <remarks>
		/// We replace here normal UTF-8 encoded special characters by converting the string from 
		/// Unicode to UTF-8 and than from UTF-8 to Win-1252.
		/// Characters, that are encoded by escape sequences like \195\164 are converted from escape
		/// sequences to normal byte format.
		/// In this function, we assume, that the characters beyond ASCII 127 are not obfuscated.
		/// </remarks>
		/// <returns>New Lua code.</returns>
		/// <param name="luaCode">Lua code.</param>
		string ReplaceSpecialCharacters (string luaCode)
		{
			// Convert C# Unicode string to UTF-8, its native format
			byte[] input = Encoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(luaCode));
			byte[] output = new byte[input.Length];

			// Now check for byte combinations like \128 to \255
			int posInput = 0;
			int posOutput = 0;
			while(posInput < input.Length-3) {
				if (input[posInput] == 92 && input[posInput+1] == 49 && input[posInput+2] >= 48 && input[posInput+2] <= 57 && input[posInput+3] >= 48 && input[posInput+3] <= 57) {
					// Ok, we have a \ followed by three numbers
					byte value = (byte)(100 + (input[posInput+2] - 48) * 10 + (input[posInput+3] - 48));
					if (value > 127) {
						// We have a UTF-8 escape sequence, so replace 4 bytes with the new one
						output[posOutput++] = value;
						posInput += 4;
					} else {
						// It's not a UTF-8 escape sequence, so don't replace it
						output[posOutput++] = input[posInput++];
						output[posOutput++] = input[posInput++];
						output[posOutput++] = input[posInput++];
						output[posOutput++] = input[posInput++];
					}
				} else {
					output[posOutput++] = input[posInput++];
				}
			}

			while(posInput < input.Length)
				output[posOutput++] = input[posInput++];

			// Resize array, because it could get shorter than before
			Array.Resize(ref output, posOutput);

			// Convert UTF-8 byte array to C# string encoded in Win-1252
			return _encodingWin1252.GetString(Encoding.Convert(Encoding.UTF8, _encodingWin1252, output));
		}

		string ReplaceEscape(Match match)
		{
			short value;

			if (Int16.TryParse(match.Groups[1].Value.Substring(1), out value)) {
				if (value > 127) {
					return ((char)value).ToString();
				} else {
					return match.Groups[1].Value;
				}
			} else {
				return match.Groups[1].Value;
			}
		}

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