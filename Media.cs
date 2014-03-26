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
using Ionic.Zip;
using System.Collections.Generic;

namespace WF.Compiler
{
	public enum MediaFormat { bmp=1, png=2, jpg=3, gif=4, wav=17, mp3=18, fdl=19, ogg=21 };

	public static class MediaHelpers
	{
		public static bool IsImage(this MediaFormat mf)
		{
			return (mf == MediaFormat.bmp || mf == MediaFormat.png || mf == MediaFormat.jpg || mf == MediaFormat.gif);
		}

		public static bool IsSound(this MediaFormat mf)
		{
			return (mf == MediaFormat.wav || mf == MediaFormat.mp3 || mf == MediaFormat.fdl || mf == MediaFormat.ogg);
		}
	}

	public class MediaResource
	{
		public MediaFormat Type;
		public string Filename;
		public List<String> Directives = new List<String> ();
		public byte[] Data;
	}

	public class Media
	{
		public string Variable;
		public string Name;
		public string Description;
		public string AltText;
		public string Id;
		public MediaResource Resource;
		public List<MediaResource> Resources = new List<MediaResource> ();
    }
}

