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
using WF.Compiler;

namespace Groundspeak.Wherigo.ZonesLinker
{
	public enum DeviceType { Unknown = 0, PPC2003, Garmin, WhereYouGo, OpenWIG, XMarksTheSpot, iPhone, iPhoneRetina, iPad, iPadRetina, Emulator, WFPlayer };
	public enum EngineVersion { V0210 = 1, V0211 };

	public class ZonesLinker
	{
		public ZonesLinker ()
		{
		}

		public void CreateZonesFile(string inputFilename, string outputFilename, string guid, string userName, long userId, string completitionCode, DeviceType device, EngineVersion version)
		{
            FileStream inputStream = null;
            Cartridge cartridge;

            try
            {
                // Open Lua file
                inputStream = new FileStream(inputFilename, FileMode.Open);

                // Create input object for plain folders
                IInput input = new Folder(inputStream, inputFilename);

                // Check Lua file
                input.Check();

                // Load Lua code and extract all required data
                cartridge = input.Load();

                // Close input
                input = null;
            }
            finally
            {
                if (inputStream != null)
                {
                    inputStream.Close();
                    inputStream = null;
                }
            }

			// Create selected engine
			IEngine engine = Compiler.CreateEngine(device);

			// Convert Lua code and insert special code for this player
			cartridge = engine.ConvertCartridge(cartridge);
			userName = engine.ConvertString(userName);

			// ---------- Compile Lua code into binary chunk ----------

			// Compile Lua code
			cartridge.Chunk = LUA.Compile(cartridge.LuaCode, cartridge.LuaFileName);

			// ---------- Save cartridge as GWC file ----------

			// Create object for output format (could be also WFC or any other IOutput)
			var outputFormat = new GWC();

			// Write output file
			// Create output in correct format
			var ms = outputFormat.Create(cartridge, userName, userId, completitionCode);
			// Save output to file
			using(FileStream ofs = new FileStream(outputFilename, FileMode.Create)) {
				ms.CopyTo(ofs);
				// Close output
				ofs.Flush();
				ofs.Close();
			}
		}
	}
}

