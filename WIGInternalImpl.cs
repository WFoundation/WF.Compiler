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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Eluant;
using System.Linq;

namespace WF.Compiler
{
    /// <summary>
	/// This class implements the Wherigo libary WIGInternal.
    /// </summary>
    public class WIGInternalImpl
    {
		#region Constructor

		internal WIGInternalImpl(LuaRuntime luaState)
        {
			LuaTable wiginternal = luaState.CreateTable();
			luaState.Globals["WIGInternal"] = wiginternal;

			// Interface for GUI
			luaState.DoString("WIGInternal.LogMessage = function (a, b) end");
			luaState.DoString("WIGInternal.MessageBox = function (a, b, c, d, e) end");
			luaState.DoString("WIGInternal.GetInput = function (a) end");
			luaState.DoString("WIGInternal.NotifyOS = function (a) end");
			luaState.DoString("WIGInternal.ShowScreen = function (a, b) end");
			luaState.DoString("WIGInternal.ShowStatusText = function (a) end");

			// Events
			luaState.DoString("WIGInternal.AttributeChangedEvent = function (a, b) end");
			luaState.DoString("WIGInternal.CartridgeEvent = function (a) end");
			luaState.DoString("WIGInternal.CommandChangedEvent = function (a) end");
			luaState.DoString("WIGInternal.InventoryEvent = function (a, b, c) end");
			luaState.DoString("WIGInternal.MediaEvent = function (a, b) end");
			luaState.DoString("WIGInternal.TimerEvent = function (a, b) end");
			luaState.DoString("WIGInternal.ZoneStateChangedEvent = function (a) end");

			// Internal functions
			luaState.DoString("WIGInternal.IsPointInZone = function (a, b) end");
			luaState.DoString("WIGInternal.VectorToZone = function (a, b) end");
			luaState.DoString("WIGInternal.VectorToSegment = function (a, b, c) end");
			luaState.DoString("WIGInternal.VectorToPoint = function (a, b) end");
			luaState.DoString("WIGInternal.TranslatePoint = function (a, b, c) end");

            // Mark package WIGInternal as loaded
			//luaState.SafeSetGlobal("package.loaded.WIGInternal", wiginternal);
			//luaState.SafeSetGlobal("package.preload.WIGInternal", wiginternal);
			LuaTable package = (LuaTable)luaState.Globals["package"];
			LuaTable loaded = (LuaTable)package["loaded"];
			loaded["WIGInternal"] = wiginternal;
			loaded["io"] = null;
			LuaTable preload = (LuaTable)package["preload"];
			preload["WIGInternal"] = wiginternal;
			preload["io"] = null;

			// Deactivate
			luaState.Globals["io"] = null;

			// Set
			LuaTable env = (Eluant.LuaTable)luaState.CreateTable();
			luaState.Globals["Env"] = env;

            // Loads the Wherigo LUA engine.
			using (BinaryReader bw = new BinaryReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("WF.Compiler.Resources.Wherigo.luac")))
			{
			    byte[] binChunk = bw.ReadBytes ((int)bw.BaseStream.Length);

				luaState.DoString(binChunk, "Wherigo.lua");
			}
		}

		#endregion
    }

}
