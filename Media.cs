///
/// WF.Compiler - A Wherigo Compiler.
/// Copyright (C) 2012-2013  Dirk Weltz <web@weltz-online.de>
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

	public enum MediaFormat { bmp=1, png=2, jpg=3, gif=4, wav=17, mp3=18, fdl=19 }

	public class MediaResource
	{
		public MediaFormat Type;
		public string Filename;
		public DeviceType Device = DeviceType.Unknown; 
		public List<String> Directives = new List<String> ();

        public int GetMediaFormatAsLong()
        {
            return (int) Type;
        }
	}

	public class Media
	{
		public string Variable;
		public string Name;
		public string Description;
		public string AltText;
		public string Id;
		public int Entry = -1;  // No valid entry in the list of resources, which we can use
		public List<MediaResource> Resources = new List<MediaResource> ();

        public int MediaType { get { return Entry > -1 ? (int)Resources[Entry].Type : 0; } }

        /// <summary>
        /// Extract the file which belongs to the entry as byte array.
        /// </summary>
        /// <param name="zip">ZipFile where to extract the data from.</param>
        /// <returns>Byte array with the data for the image entry. If entry is -1, than it returns null.</returns>
        public byte[] GetMediaAsByteArray(ZipFile zip)
        {
            byte[] result = null;
            string filename = Entry > -1 ? Resources[Entry].Filename : null;

            if ( filename != null )
            {
                BinaryReader br = new BinaryReader(zip[filename].OpenReader());
                result = new byte[(int.MaxValue < zip[filename].UncompressedSize ? int.MaxValue : (int)zip[filename].UncompressedSize)];
                br.Read(result, 0, (int.MaxValue < result.Length ? int.MaxValue : (int)result.Length));
                br.Close();
            }

            return result;
        }
        
    }

}

