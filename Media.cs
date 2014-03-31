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
	public enum MediaType 
	{
		Unknown = 0,
		BMP = 1,
		PNG = 2,
		JPG = 3,
		GIF = 4,
		WAV = 17,
		MP3 = 18,
		FDL = 19,
		SND = 20,
		OGG = 21,
		SWF = 33,
		TXT = 49
	}

	public static class MediaHelpers
	{
		public static bool IsImage(this MediaType mf)
		{
			return (mf == MediaType.BMP || mf == MediaType.PNG || mf == MediaType.JPG || mf == MediaType.GIF);
		}

		public static bool IsSound(this MediaType mf)
		{
			return (mf == MediaType.WAV || mf == MediaType.MP3 || mf == MediaType.FDL || mf == MediaType.SND || mf == MediaType.OGG);
		}

		public static bool IsText(this MediaType mf)
		{
			return (mf == MediaType.TXT);
		}
	}

	public class MediaResource
	{
		public MediaType Type;
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

