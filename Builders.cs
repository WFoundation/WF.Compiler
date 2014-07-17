// ///
// /// WF.Player - A Wherigo Player, which use the Wherigo Foundation Core.
// /// Copyright (C) 2012-2014  Dirk Weltz <mail@wfplayer.com>
// ///
// /// This program is free software: you can redistribute it and/or modify
// /// it under the terms of the GNU Lesser General Public License as
// /// published by the Free Software Foundation, either version 3 of the
// /// License, or (at your option) any later version.
// /// 
// /// This program is distributed in the hope that it will be useful,
// /// but WITHOUT ANY WARRANTY; without even the implied warranty of
// /// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// /// GNU Lesser General Public License for more details.
// /// 
// /// You should have received a copy of the GNU Lesser General Public License
// /// along with this program.  If not, see <http://www.gnu.org/licenses/>.
// ///
//
using System;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WF.Compiler
{
	public static class Builders
	{
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

		#region Urwigo

		/// <summary>
		/// Gets the urwigo obfuscation function name.
		/// </summary>
		/// <returns>The urwigo obfuscation function.</returns>
		/// <param name="lua">Lua code.</param>
		public static string GetUrwigoObfuscationFunction(string lua)
		{
			// Check for function and dtable
			Match dtable = Regex.Match (lua, @"function\s*([_a-zA-Z0-9]*)\s*\(str\).*dtable\s*=\s*", RegexOptions.Singleline); //.*dtable\\s*=\\s*\\x22((\\x5c\\d{3})*)\\x22", RegexOptions.Singleline);

			string result = "";

			if (dtable.Success)
			{
				// Extract function name
				result = dtable.Groups[1].Value;
			}

			return result;
		}

		/// <summary>
		/// Gets the Urwigo obfuscation table.
		/// </summary>
		/// <returns>The Urwigo obfuscation table.</returns>
		/// <param name="lua">Lua code.</param>
		public static List<int> GetUrwigoObfuscationTable(string lua)
		{
			List <int> result = new List<int>();

			// Check for function and dtable
			Match dtable = Regex.Match(lua, @"function\s*([_a-zA-Z0-9]*)\s*\(str\).*dtable\s*=\s*""(.*?)""", RegexOptions.Singleline);

			if (dtable.Success)
			{
				dtable = Regex.Match(lua, @"^\s*local dtable\s*=\s*""(.*?)""\s*$", RegexOptions.Multiline);
				// Extract bytes in dtable
				string formatted = GetUrwigoFormatedString (dtable.Groups [1].Value);
				// Remove leading newline + cr
				// if (formatted.StartsWith(@"\013"))
				//   formatted = formatted.Substring (8);
				// Replace newline + cr with newline
				// formatted = formatted.Replace (@"\013\010", @"\010");
				MatchCollection bytes = Regex.Matches(formatted, @"\\(\d{3})*");
				for (int i = 0; i < bytes.Count; i++)
					result.Add (Int32.Parse (bytes[i].Groups[1].Value));
			}

			return result;
		}

		/// <summary>
		/// Gets the Urwigo obfuscated strings.
		/// </summary>
		/// <returns>The Urwigo obfuscated strings.</returns>
		/// <param name="lua">Lua code.</param>
		/// <param name="funcName">Func name.</param>
		public static List<string> GetUrwigoObfuscatedStrings(string lua, string funcName)
		{
			List<string> result = new List<string> ();

			// Extract all obfuscated strings
			MatchCollection obfs = Regex.Matches(lua, funcName + @"\s*\(""([^)]*)""\)", RegexOptions.Singleline);
			foreach (Match m in obfs)
				if (!String.IsNullOrEmpty(m.Groups[1].Value))
					result.Add(m.Groups[1].Value);

			// Add strings with [[]] as string delimiter
			obfs = Regex.Matches(lua, funcName + @"\s*\(\[(=*)\[([^)]*)\]\1\]\)", RegexOptions.Singleline);
			foreach (Match m in obfs)
				if (!String.IsNullOrEmpty(m.Groups[2].Value))
					result.Add(m.Groups[2].Value);

			return result;
		}

		/// <summary>
		/// Gets the Urwigo formated string, like \032\045, from a normal byte containing string.
		/// </summary>
		/// <remarks>
		/// The string could also contain \032 from beginning, so the function has to check for this.
		/// </remarks>
		/// <returns>The Urwigo formated string.</returns>
		/// <param name="obf">Obf.</param>
		public static string GetUrwigoFormatedString(string byteCode)
		{
			StringBuilder backslashCode = new StringBuilder();
			int i = 0;
			// Replace standard escape sequences
			byteCode = byteCode.Replace(@"\\",@"\092").Replace(@"\a",@"\007").Replace(@"\b",@"\008").Replace(@"\f",@"\012").Replace(@"\n",@"\010");
			byteCode = byteCode.Replace(@"\r",@"\013").Replace(@"\t",@"\009").Replace(@"\v",@"\011").Replace(@"\""",@"\034").Replace(@"\'",@"\039");
			// Now replace all characters with the backslash notation
			while (i < byteCode.Length) {
				if (byteCode.Substring(i,1) == "\\" && i+3 < byteCode.Length && String.Compare(byteCode.Substring(i+1,3), "000") >= 0 && String.Compare(byteCode.Substring(i+1,3), "999") <= 0) {
					backslashCode.Append(byteCode.Substring(i,4));
					i += 4;
				} else {
					backslashCode.Append(String.Format("\\{0:000}", Convert.ToByte((char)byteCode.Substring(i, 1)[0] & 0xFF)));
					i++;
				}
			}
			return backslashCode.ToString ();
		}

		/// <summary>
		/// Gets the Urwigo string decoded.
		/// </summary>
		/// <returns>The Urwigo decode string.</returns>
		/// <param name="str">String.</param>
		/// <param name="dtable">Dtable.</param>
		public static string GetUrwigoDecodeString(string str, List<int> dtable)
		{
			StringBuilder result = new StringBuilder();

			// Remove leading newline + cr
			// if (str.StartsWith(@"\013"))
			//   str = str.Substring (8);
			// Replace newline + cr with newline
			//			str = str.Replace (@"\013\010", @"\010");

			// Extract bytes in dtable
			MatchCollection bytes = Regex.Matches(str, @"\\(\d{3})*");
			for (int i = 0; i < bytes.Count; i++) {
				byte b = Byte.Parse (bytes [i].Groups [1].Value);
				if (b > 0 && b <= 127)
					result.Append ((char)dtable [b - 1]);
				else
					result.Append ((char)b);
			}

			return result.ToString ();
		}

		#endregion

		#region Earwigo

		/// <summary>
		/// Removes the Urwigo obfuscation function and replace all strings with plain text.
		/// </summary>
		/// <returns>The urwigo obfuscation.</returns>
		/// <param name="lua">Lua code.</param>
		public static string RemoveEarwigoObfuscation(string lua)
		{
			string result = lua;
			string funcName = "WWB_deobf";

			result = Regex.Replace (result, funcName + @"\s*\(""([^""]*)""\)", delegate(Match match) {
				return @"""" + GetEarwigoEncodedString(match.Groups[1].Value) + @"""";
			}, RegexOptions.Singleline);

			return result;
		}

		/// <summary>
		/// Decode the Earwigo string.
		/// </summary>
		/// <returns>The Earwigo decoded string.</returns>
		/// <param name="str">String to decode.</param>
		/// <param name="dtable">Dtable.</param>
		public static string GetEarwigoDecodedString(string str)
		{
			str = str.Replace(@"&nbsp;", @" ").Replace(@"&lt;", @"\004").Replace(@"&gt;", @"\005").Replace(@"&amp;", @"\006");

			var result = GetEarwigoObfuscatedString(str, false);

			result = result.Replace (@"\004", @"&lt;").Replace (@"\005", @"&gt;").Replace (@"\006", @"&amp;");

			return result;
		}

		/// <summary>
		/// Encode the Earwigo string.
		/// </summary>
		/// <returns>The Earwigo encoded string.</returns>
		/// <param name="str">String.</param>
		/// <param name="dtable">Dtable.</param>
		public static string GetEarwigoEncodedString(string str)
		{
			str = str.Replace(@"&nbsp;", @" ").Replace(@"&lt;", @"\004").Replace(@"&gt;", @"\005").Replace(@"&amp;", @"\006");

			var result = GetEarwigoObfuscatedString(str, true);

			result = result.Replace (@"\004", @"&lt;").Replace (@"\005", @"&gt;").Replace (@"\006", @"&amp;");

			return result;
		}


		/// <summary>
		/// Encode/Decode the Earwigo string.
		/// </summary>
		/// <returns>The Earwigo encoded/decoded string.</returns>
		/// <param name="str">String.</param>
		/// <param name="dtable">Dtable.</param>
		private static string GetEarwigoObfuscatedString(string str, Boolean encrypt)
		{
			StringBuilder result = new StringBuilder();
			string rot_palette = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@.-~";
			int plen = rot_palette.Length;
			int pos = 0;			// Counter for the position in the byte string, which is different from position in string (because of the escape sequences) 

			for (int i = 0; i < str.Length; i++) {
				// Get next character
				char c = Convert.ToChar(str.Substring(i, 1));
				// Is character a backslash?
				if (c == 92 && i+3 < str.Length && String.Compare(str.Substring(i+1,3), "000") >= 0 && String.Compare(str.Substring(i+1,3), "999") <= 0) {
					// Add code
					result.Append(str.Substring(i, 4));
					i = i + 3;
					// Counts only fo one character, even if it uses 4 bytes
					pos++;
				} else {
					// Get correct position
					int p = rot_palette.IndexOf(c);
					// Do the magic
					if (p > 0) {
						int jump = pos % 8 + 9;
						if (encrypt) {
							p = p + jump;
							if (p >= plen)
								p = p - plen;
						} else {
							p = p - jump;
							if (p < 0)
								p = p + plen;
						}
						c = Convert.ToChar (rot_palette.Substring (p, 1));
					}
					result.Append(c);
					pos++;
				}
			}

			return result.ToString();
		}

		#endregion
	}
}

