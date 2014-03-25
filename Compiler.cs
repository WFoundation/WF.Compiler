///
/// WF.Compiler - A Wherigo Compiler.
/// Copyright (C) 2012-2014  Dirk Weltz <web@weltz-online.de>
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
using System.Text.RegularExpressions;
using Ionic.Zip;
using Eluant;
using System.Collections.Generic;

namespace WF.Compiler
{
    public class Compiler
    {
        public static void Main(string[] args)
        {
			var start = DateTime.Now;
			var fileInput = @"S:\Entwicklung\CSharp\WF.Compiler\Bebenhausen.gwz"; // Geocaching\Wherigo\Bebenhausen\Bebenhausen.gwz";

			if (args.Length == 1)
				fileInput = args[0];

			var fileOutput = Path.ChangeExtension(fileInput,".gwc");

			int userId = 0;
			string userName = "Test";
			string completitionCode = "abcdefghijk";

			FileStream ifs = new FileStream(fileInput, FileMode.Open);

			// ---------- Create GWZ file only (required for upload and download of GWZ file) ----------

			// Create object für reading input file (could be also any other format implementing IInput)
			var inputFormat = new GWZ(ifs);

			// ---------- Check GWZ file (only required for upload of GWZ file) ----------

			// Check gwz file for errors (Lua code and all files included)
			try {
				inputFormat.Check();
			}
			catch (CompilerLuaException e)
			{
				Console.WriteLine("Error at line {0}: {1}", e.Line, e.Message);
				Console.WriteLine();
				Console.WriteLine("Line {0}: {1}", e.Line-1, e.CodeBefore);
				Console.WriteLine("Line {0}: {1}", e.Line, e.Code);
				Console.WriteLine("Line {0}: {1}", e.Line+1, e.CodeAfter);
				return;
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				ifs.Close();
				return;
			}

			// ---------- Load cartridge from GWZ file (required when downloading cartridge) ----------

			// Load Lua code and extract all required data
			Cartridge cartridge = inputFormat.Load();

			// ---------- Convert cartridge for engine ----------

			// Create selected player
			IEngine engine = new EngineGarmin();

			// Convert Lua code and insert special code for this player
			cartridge = engine.ConvertCartridge(cartridge);
			userName = engine.ConvertString(userName);

			// Now we can close the input, because we don't require it anymore
			ifs.Close();

			// ---------- Compile Lua code into binary chunk ----------

			// Compile Lua code
			cartridge.Chunk = LUA.Compile(cartridge.LuaCode, cartridge.LuaFileName);

			// ---------- Save cartridge as GWC file ----------

			// Create object for output format (could be also WFC or any other IOutput)
			var outputFormat = new GWC();

			// Write output file
			try {
				// Create output in correct format
				var ms = outputFormat.Create(cartridge, userName, userId, completitionCode);
				// Save output to file
				using(FileStream ofs = new FileStream(fileOutput, FileMode.Create)) {
					ms.CopyTo(ofs);
					// Close output
					ofs.Flush();
					ofs.Close();
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				return;
			}

			Console.WriteLine("Time: {0}", DateTime.Now - start);
        }
    }
}

