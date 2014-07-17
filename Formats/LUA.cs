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
using System.Collections.Generic;
using System.IO;
using Eluant;
using System.Text.RegularExpressions;

namespace WF.Compiler
{
	/// <summary>
	/// This static libray contains all function for handling the Lua source code.
	/// </summary>
	public static class LUA
	{
		/// <summary>
		/// Check the specified luaCode in stream to see, if there are any errors.
		/// </summary>
		/// <param name="luaCode">Stream with Lua code to check.</param>
		public static void Check(Stream stream, string luaFileName)
		{
			// Get Lua code
			string luaCode = new StreamReader(stream).ReadToEnd();

			Check(luaCode, luaFileName);
		}
		/// <summary>
		/// Check the specified luaCode to see, if there are any errors.
		/// </summary>
		/// <param name="luaCode">String with Lua code to check.</param>
		public static void Check(string luaCode, string luaFileName)
		{
			// Create Lua runtime
			LuaRuntime luaState = new LuaRuntime ();

			// Check Lua for errors
			try {
				// Try to compile the code
				luaState.CompileString (luaCode, luaFileName);
			}
			catch (Exception ex)
			{
				// There are errors in the Lua code
				// So raise an error with the error data
				Match match = Regex.Match(ex.Message, @"(.*):(\d*):(.*)");
				int line = Convert.ToInt32(match.Groups[2].Value);
				string message = String.Format("{0}, line {1}: {2}", match.Groups[1].Value.Replace("string", "File"), match.Groups[2].Value, match.Groups[3].Value);
				string error = match.Groups[3].Value;
				string[] lines = luaCode.Replace("\r","").Split('\n');
				string code = lines.Length >= line-1 ? lines[line-1] : null;
				string before = lines.Length >= line-2 ? lines[line-2] : null;
				string after = lines.Length >= line ? lines[line] : null;

				throw new CompilerLuaException(message, line, error, code, before, after);
			}

			luaState = null;
		}

		/// <summary>
		/// Extract cartridge data from specified luaCode in given stream.
		/// </summary>
		/// <param name="luaCode">Stream with Lua code.</param>
		public static Cartridge Extract(Stream stream)
		{
			// Get Lua code
			string luaCode = new StreamReader(stream).ReadToEnd();

			return Extract(luaCode);
		}

		/// <summary>
		/// Extract cartridge data from the given specified luaCode in given string.
		/// </summary>
		/// <param name="luaCode">String with Lua code.</param>
		public static Cartridge Extract(string luaCode)
		{
			LuaTable ltMedia = null;
			LuaTable ltIcon = null;

			// Create Lua runtime
			LuaRuntime luaState = new Eluant.LuaRuntime ();

			// Load Wherigo.lua
			var wigInternal = new WIGInternalImpl(luaState);

			// Run Lua
			var cart = (LuaTable)((LuaVararg)luaState.DoString(luaCode, "Cartridge"))[0];

			if (cart == null)
				throw new ArgumentNullException("ZCartridge");

			// Get table for all ZObjects
			var objs = (LuaTable)cart["AllZObjects"];

			if (objs == null)
				throw new ArgumentException("AllZObjects");

			// Get all relevant data for cartridge
			Cartridge result = new Cartridge();

			// Now we check the Lua code for old newlines like <BR>\n or long strings with only a \n as content
			// All this should replaced by normal \n newlines
			result.LuaCode = ConvertToPlain(luaCode);

			// Extract variable for cartridge
			var regex = new Regex(@"return\s*([_0-9a-zA-Z]*)\s*$", RegexOptions.Singleline & RegexOptions.IgnoreCase);
			var match = regex.Match(luaCode);

			if (match.Success)
				result.Variable = match.Groups[1].Value;

			result.Id = (LuaString)cart["Id"];
			result.Name = cart["Name"].ToString();
			result.Description = cart["Description"].ToString();
			result.Activity = cart["Activity"].ToString();
			result.StartingLocationDescription = cart["StartingLocationDescription"].ToString();
			LuaTable zp = cart["StartingLocation"] is LuaBoolean ? null : (LuaTable)cart["StartingLocation"];
			result.Latitude = zp == null ? 0 : (double)((LuaNumber)zp["latitude"]).ToNumber();
			result.Longitude = zp == null ? 0 : (double)((LuaNumber)zp["longitude"]).ToNumber();
			result.Altitude = zp == null ? 0 : (cart["StartingLocation"] is LuaBoolean ? 0 : (double)((LuaNumber)((LuaTable)zp["altitude"])["value"]).ToNumber());
			result.Version = cart["Version"].ToString();
			result.Company = cart["Company"].ToString();
			result.Author = cart["Author"].ToString();
			result.BuilderVersion = cart["BuilderVersion"].ToString();
			result.CreateDate = cart["CreateDate"].ToString();
			result.PublishDate = cart["PublishDate"].ToString();
			result.UpdateDate = cart["UpdateDate"].ToString();
			result.LastPlayedDate = cart["LastPlayedDate"].ToString();
			result.TargetDevice = cart["TargetDevice"].ToString();
			result.TargetDeviceVersion = cart["TargetDeviceVersion"].ToString();
			result.StateId = cart["StateId"].ToString();
			result.CountryId = cart["CountryId"].ToString();
			result.Visible = ((LuaBoolean)cart["Visible"]).ToBoolean();
			result.Complete = ((LuaBoolean)cart["Complete"]).ToBoolean();
			result.UseLogging = ((LuaBoolean)cart["UseLogging"]).ToBoolean();
			ltMedia = cart["Media"] is LuaBoolean ? null : (LuaTable)cart["Media"];
			ltIcon = cart["Icon"] is LuaBoolean ? null : (LuaTable)cart["Icon"];

			// Check for medias of cartridge
			var table = (LuaTable)luaState.Globals["table"];
			var contains = (LuaFunction)table["Contains"];

			var p = contains.Call(cart["AllZObjects"], ltMedia);
			if (p[0] is LuaBoolean && p[0].ToBoolean())
				result.Poster = p[1] is LuaNumber ? (int)((LuaNumber)p[1]) : -1;
			else
				result.Poster = -1;

			var i = contains.Call(cart["AllZObjects"], ltIcon);
			if (i[0] is LuaBoolean && i[0].ToBoolean())
				result.Icon = i[1] is LuaNumber ? (int)((LuaNumber)i[1]) : -1;
			else
				result.Icon = -1;

			// Get all other medias
			foreach(KeyValuePair<LuaValue, LuaValue> pair in objs) {
				int idx = (int)(LuaNumber)pair.Key;
				LuaTable obj = ((LuaTable)pair.Value);
				// Get type of ZObject
				string className = (LuaString)obj["ClassName"];
				string name = (LuaString)obj["Name"];
				// Check type of ZObject
				if (className.Equals("ZMedia")) {
					Media media = ExtractMedia (obj);
					result.Medias.Add (media);
				}
			}

			// Get all variable names
			foreach(KeyValuePair<LuaValue, LuaValue> pair in luaState.Globals) {
				var k = pair.Key;
				var v = pair.Value;
				if (v is LuaTable) {
					var className = ((LuaTable)v)["ClassName"];
					if (className != null && className.ToString().Equals("Zone"))
						result.Zones.Add(k.ToString());
					if (className != null && className.ToString().Equals("ZItem"))
						result.Items.Add(k.ToString());
					if (className != null && className.ToString().Equals("ZCharacter")) {
						if (!k.ToString().Equals("Player")) 
							result.Characters.Add(k.ToString());
					}
					if (className != null && className.ToString().Equals("ZTimer"))
						result.Timers.Add(k.ToString());
					if (className != null && className.ToString().Equals("ZInput"))
						result.Inputs.Add(k.ToString());
					if (className != null && className.ToString().Equals("ZCartridge"))
						result.Variable = k.ToString();
				}
			}

			return result;
		}

		public static byte[] Compile(string luaCode, string fileName = "Cartridge")
		{
			// Create Lua runtime
			LuaRuntime luaState = new Eluant.LuaRuntime ();

			// Load Wherigo.lua
			var wigInternal = new WIGInternalImpl(luaState);

			// Get string.dump() function
			LuaTable stringTable = (LuaTable)luaState.Globals["string"];
			LuaFunction stringDump = (LuaFunction)stringTable["dump"];

			// Compile Lua code
			LuaFunction lf = luaState.CompileString(luaCode, fileName);

			// Retrive Lua code by string.dump
			var ret = stringDump.Call(new List<LuaValue>() {lf});

			if (ret.Count >= 1) {
				string byteArray = (LuaString)ret[0];
				byte[] result = new byte[byteArray.Length];
				for (int i = 0; i < byteArray.Length; i++)
					result[i] = (byte)(byteArray[i] & 0xFF);
				return result;
			} else {
				return null;
			}
		}

		#region Private Functions

		/// <summary>
		/// Converts the original Lua code to a plain version.
		/// </summary>
		/// <remarks>
		/// Replace all 
		/// - <BR>\n with \n 
		/// - <BR> with \n
		/// - long strings with only a \n with short strings containing \n
		/// This works although for Urwigo and Earwigo encoded strings. 
		/// They are decoded, changed and encoded again.
		/// </remarks>
		/// <returns>The to plain.</returns>
		/// <param name="luaCode">Lua code.</param>
		static string ConvertToPlain (string luaCode)
		{
			string result = luaCode;

			// Replace all the <BR> constructions
			//			result = Regex.Replace(result, "\r\n|\n\r", "\n");
			result = result.Replace(@"<BR>\n", @"\n");
			result = result.Replace(@"\0+60BR\0+62\n", @"\n");
			result = result.Replace(@"\0+60BR\0+62\010", @"\n");
			result = result.Replace(@"\0+60BR\0+62", @"\n");

			// Replace long strings containing only a newline with a short string with a newline
			result = Regex.Replace(result, @"\[(=*)\[\n\]\1\]", "\"\\n\"");
			result = Regex.Replace(result, @"\[(=*)\[(\r\n|\n\r)\]\1\]", "\"\\n\"");

			if (Builders.IsUrwigo(result)) {
				// Ok, it is a Lua file created by Urwigo.
				// Here we don't check, if the string is encoded or not. We assume, that this escape 
				// sequences only in the code where strings are obfuscated.
				// Get the dtable of the obfuscatition function
				List<int> dtable = Builders.GetUrwigoObfuscationTable(result);
				// Get the escape sequences for <, >, BR and \n
				// This only works, if the obfuscated string is coded by escape sequences with three valid digits.
				string lt = String.Format("\\{0:000}", dtable.IndexOf('<'));
				string gt = String.Format("\\{0:000}", dtable.IndexOf('>'));
				string cr = String.Format("\\{0:000}", dtable.IndexOf('\r'));
				string nl = String.Format("\\{0:000}", dtable.IndexOf('\n'));
				string br = String.Format("\\{0:000}\\{1:000}", dtable.IndexOf('B'), dtable.IndexOf('R'));
				// Replace all the different occurences of <BR>
				result = Regex.Replace(result, cr+nl+"|"+nl+cr, nl);
				result = result.Replace(lt+br+gt+nl, nl);
			}

			if (Builders.IsEarwigo(result)) {
				// Ok, it is a Lua file created by Earwigo
				// Search for all obfuscated strings
				result = Regex.Replace(result, @"WWB_deobf\((""|')(.*?)\1\)", delegate (Match match) {
//					string encoded = match.Groups[2].Value;
//					string decoded = Builders.GetEarwigoDecodedString(encoded);
//					decoded = decoded.Replace(@"<BR>\n", @"\n");
//					decoded = decoded.Replace(@"\0+60BR\0+62\n", @"\n");
//					decoded = decoded.Replace(@"\0+60BR\0+62\010", @"\n");
//					decoded = decoded.Replace(@"\0+60BR\0+62", @"\n");
//					encoded = Builders.GetEarwigoEncodedString(decoded);
					return String.Format(@"WWB_deobf({0}{1}{0})", match.Groups[1].Value, match.Groups[2].Value.Replace(@"\060\001\002\062\010\003\003\003\003", @"\010"));
					}, RegexOptions.Singleline);
			}

			return result;
		}

		/// <summary>
		/// Extracts the media from the Lua code.
		/// </summary>
		/// <param name="obj">Object.</param>
		static Media ExtractMedia (LuaTable obj)
		{
			Media result = new Media ();

			// Extract table entries
			result.Name = obj ["Name"].ToString();
			result.Id = obj ["Id"].ToString();
			result.Description = obj ["Description"].ToString();
			result.AltText = obj ["AltText"].ToString();
			// Get resources
			result.Resources = obj ["Resources"] is LuaBoolean ? null : ExtractMediaResources ((LuaTable)obj ["Resources"]);

			return result;
		}

		/// <summary>
		/// Extracts the media resources from Lua.
		/// </summary>
		/// <returns>The media resources.</returns>
		/// <param name="resources">LuaTable with resources.</param>
		static List<MediaResource> ExtractMediaResources (LuaTable resources)
		{
			List<MediaResource> result = new List<MediaResource>();

			// Do it for each entry of the Lua table
			foreach (KeyValuePair<LuaValue, LuaValue> pair in resources) {
				// Get table for this resource
				LuaTable resTable = (LuaTable)pair.Value;
				// Create a new media resource object
				MediaResource mediaResource = new MediaResource ();
				// Extract table entries
				mediaResource.Filename = resTable["Filename"].ToString();
				string type = resTable["Type"].ToString();
				if (String.IsNullOrEmpty(type) && !String.IsNullOrEmpty(mediaResource.Filename) && Path.HasExtension(mediaResource.Filename))
					type = Path.GetExtension(mediaResource.Filename);
				switch(type.ToLower()) {
				case "bmp":
					mediaResource.Type = MediaType.BMP;
					break;
				case "jpg":
					mediaResource.Type = MediaType.JPG;
					break;
				case "png":
					mediaResource.Type = MediaType.PNG;
					break;
				case "gif":
					mediaResource.Type = MediaType.GIF;
					break;
				case "wav":
					mediaResource.Type = MediaType.WAV;
					break;
				case "mp3":
					mediaResource.Type = MediaType.MP3;
					break;
				case "fdl":
					mediaResource.Type = MediaType.FDL;
					break;
				case "snd":
					mediaResource.Type = MediaType.SND;
					break;
				case "ogg":
					mediaResource.Type = MediaType.OGG;
					break;
				case "swf":
					mediaResource.Type = MediaType.SWF;
					break;
				case "txt":
					mediaResource.Type = MediaType.TXT;
					break;
				}
				// Get directives for this media resource entry
				mediaResource.Directives = resTable ["Directives"] is LuaBoolean ? null : ExtractDirectives ((LuaTable)resTable ["Directives"]);
				// Save new media resource
				result.Add (mediaResource);
			}

			return result;
		}

		/// <summary>
		/// Extracts the directives for a given media resource.
		/// </summary>
		/// <returns>The directives.</returns>
		/// <param name="directives">LuaTable with Directives.</param>
		static List<string> ExtractDirectives(LuaTable directives)
		{
			List<string> result = new List<string>();

			// Do it for each entry of the Lua table
			foreach(KeyValuePair<LuaValue, LuaValue> pair in directives) {
				// Add each string in the Lua table
				if (pair.Key is LuaString)
					result.Add(pair.Key.ToString().ToLower());
			}

			return result;
		}

		#endregion
	}

	public class CompilerLuaException : Exception
	{
		int _line;
		string _error;
		string _code;
		string _codeBefore;
		string _codeAfter;

		#region Constructor

		public CompilerLuaException(string message, int line, string error, string code, string before, string after) : base(message)
		{
			_line = line;
			_error = error;
			_code = code;
			_codeBefore = before;
			_codeAfter = after;
		}

		#endregion

		#region Members

		public int Line
		{
			get { return _line; }
		}

		public string Error
		{
			get { return _error; }
		}

		public string Code
		{
			get { return _code; }
		}

		public string CodeBefore
		{
			get { return _codeBefore; }
		}

		public string CodeAfter
		{
			get { return _codeAfter; }
		}

		#endregion

	}
}

