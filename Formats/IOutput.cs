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

namespace WF.Compiler
{
	public interface IOutput
	{
		/// <summary>
		/// Create an output file for the specified player, cartridge, username, userId and completionCode.
		/// </summary>
		/// <param name="player">Player.</param>
		/// <param name="cartridge">Cartridge.</param>
		/// <param name="username">Username.</param>
		/// <param name="userId">User identifier.</param>
		/// <param name="completionCode">Completion code.</param>
		MemoryStream Create (Cartridge cartridge, string username, long userId, string completionCode);
	}
}

