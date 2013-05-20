//
//  WF Compiler
//  Copyright (C) 2012-2013  Dirk Weltz <web@weltz-online.de>
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
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
using System.Collections.Generic;


namespace WF.Compiler
{
	public class Cartridge
	{
		public string Variable;
		public string Id = "";
		public string Name = "";
        public double Latitude = 0;
        public double Longitude = 0;
        public double Altitude = 0;
		public string Description = "";
		public bool Visible;
		public string Activity = "";
		public string StartingLocationDescription = "";
		public string StartingLocation;
		public string Version = "";
		public string Company = "";
		public string Author = "";
		public string BuilderVersion;
		public string CreateDate;
		public string PublishDate;
		public string UpdateDate;
		public string LastPlayedDate;
		public string TargetDevice = "";
		public string TargetDeviceVersion;
		public string StateId;
		public string CountryId;
		public bool Complete;
		public bool UseLogging;
		public string Splash;
		public string Icon;

		public List<Media> MediaList = new List<Media> ();

		public Cartridge ()
		{
		}
	}
}

