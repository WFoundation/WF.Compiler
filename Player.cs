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
using System.Text;
using System.Text.RegularExpressions;


namespace WF.Compiler
{
	public enum DeviceType { Unknown, Garmin, Colorado, Oregon, PocketPC, WhereYouGo, DesktopWIG, OpenWIG, XMarksTheSpot, iPhone, iPad, Emulator };

	public class Player
	{
		private DeviceType device;
		private string deviceName;

        private Regex regexUTF8Code = new Regex(@"\\([0-9][0-9][0-9])", RegexOptions.Compiled | RegexOptions.Singleline);

		private static MediaFormat[] mediaFormatsGarmin = { MediaFormat.bmp, MediaFormat.jpg, MediaFormat.fdl };
		private static MediaFormat[] mediaFormatsColorado = { MediaFormat.bmp, MediaFormat.jpg, MediaFormat.fdl };
		private static MediaFormat[] mediaFormatsOregon = { MediaFormat.bmp, MediaFormat.jpg, MediaFormat.fdl };
		private static MediaFormat[] mediaFormatsPocketPC = { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };
		private static MediaFormat[] mediaFormatsWhereYouGo = { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };
		private static MediaFormat[] mediaFormatsDesktopWIG = { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };
		private static MediaFormat[] mediaFormatsOpenWIG = { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };
		private static MediaFormat[] mediaFormatsXMarksTheSpot = { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };
		private static MediaFormat[] mediaFormatsiPhone = { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };
		private static MediaFormat[] mediaFormatsiPad = { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };
		private static MediaFormat[] mediaFormatsEmulator = { MediaFormat.bmp, MediaFormat.png, MediaFormat.jpg, MediaFormat.gif, MediaFormat.wav, MediaFormat.mp3 };
		private static MediaFormat[][] deviceFormats = { new MediaFormat[] {},
			                                      mediaFormatsGarmin, 
												  mediaFormatsColorado, 
												  mediaFormatsOregon, 
												  mediaFormatsPocketPC,
												  mediaFormatsWhereYouGo,
												  mediaFormatsDesktopWIG,
												  mediaFormatsOpenWIG,
												  mediaFormatsXMarksTheSpot,
												  mediaFormatsiPhone,
												  mediaFormatsiPad,
												  mediaFormatsEmulator }; 
		
        private static Encoding[] deviceEncoding = { Encoding.UTF8, 
													 Encoding.GetEncoding(1252),
													 Encoding.GetEncoding(1252),
													 Encoding.GetEncoding(1252),
													 Encoding.UTF8, 
													 Encoding.UTF8, 
													 Encoding.UTF8, 
													 Encoding.UTF8, 
													 Encoding.UTF8, 
													 Encoding.UTF8, 
													 Encoding.UTF8, 
													 Encoding.UTF8 };

        private static string[] deviceCode = { null, 
											 "CodeGarmin.lua",
											 "CodeGarmin.lua",
											 "CodeGarmin.lua",
											 null, 
											 null, 
											 null, 
											 null, 
											 null, 
											 null, 
											 null, 
											 null };

        private delegate T convertString<T>(string text);

        private convertString<string>[] deviceConvertString = { null,
                                                       convertStringForGarmin,
                                                       convertStringForGarmin,
                                                       convertStringForGarmin,
                                                       null,
                                                       null,
                                                       null,
                                                       null,
                                                       null,
                                                       null,
                                                       null,
                                                       null
                                                     };

        private delegate T convertGWCString<T>(string text);

        private convertGWCString<string>[] deviceConvertGWCString = { null,
                                                       convertGWCStringForGarmin,
                                                       convertGWCStringForGarmin,
                                                       convertGWCStringForGarmin,
                                                       null,
                                                       null,
                                                       null,
                                                       null,
                                                       null,
                                                       null,
                                                       null,
                                                       null
                                                     };




		public Player ( string device ) : this ( (DeviceType) Enum.Parse ( typeof ( DeviceType ), device, true ) ) {}

		public Player ( DeviceType device )
		{
			this.device = device;
			this.deviceName = Enum.GetName ( typeof ( DeviceType ), device ).ToLower ();
		}

        /// <summary>
        /// Checks, if the media has a valid entry for this device. If yes, than return the number of the entry.
        /// </summary>
        /// <param name="media">Media with a list of entries for belonging files.</param>
        /// <returns>The number for the best entry, if there is one. Otherwise -1.</returns>
		public int CheckMedia ( Media media )
		{
			// First check, if the media allready have a valid entry
			if ( media.Entry >= 0 )
				return media.Entry;
			// Has one of the media resources a valid entry in device or contains a filename the device
			for ( int i = 0; i < media.Resources.Count; i++ )
			{
				// Has the media resource a valid device entry 
				if ( media.Resources[i].Device == device )
					return i;
				// Has the media resource the device in the filename
				if ( media.Resources[i].Filename.ToLower ().Contains ( deviceName ) )
					return i;
			}
			// No file for this device found, so check, if we could find a file, which runs on this device
			// Has one of the media resources a valid entry in device or contains a filename the device
			for ( int i = 0; i < media.Resources.Count; i++ )
			{
				// Is one of the media resources in the media formats list of the device
				if ( Array.Exists ( deviceFormats[(int) device], element => element == media.Resources[i].Type ) )
					return i;
			}

			// We didn't find any resource, which belongs to this device.
			return -1;
		}

        /// <summary>
        /// Check, if for this device there exists special code, which should be inserted into the lua binary
        /// </summary>
        /// <returns>True, if there is special code for this device</returns>
        public bool HasCode()
        {
            return deviceCode[(int)device] != null;
        }

        public string GetCode(string variable)
        {
            string result;

            if (File.Exists(deviceCode[(int)device]))
            {
                // Read text file
                TextReader tr = new StreamReader(deviceCode[(int)device]);
                result = tr.ReadToEnd();
                tr.Close();
                // Now replace all cartridge with variable
                result = result.Replace("cartridge", variable);
            }
            else
            {
                result = "";
            }

            return result;
        }

        /// <summary>
        /// Convert strings before compilation of the Lua file. 
        /// </summary>
        /// <param name="text">String which should be converted.</param>
        /// <returns>Converted string with all device dependent stuff.</returns>
		public string ConvertString ( string text )
		{
			StringBuilder result = new StringBuilder ();

            foreach ( byte b in Encoding.Convert ( Encoding.Default, deviceEncoding[(int) device], Encoding.Default.GetBytes ( text ) ) )
                result.Append ( (char) b );

            if (deviceConvertString[(int)device] != null)
                return deviceConvertString[(int)device](result.ToString ());
            else
                return result.ToString ();
		}

        /// <summary>
        /// Convert string for special Garmin tasks like linebreaks.
        /// </summary>
        /// <param name="text">Text with the right device encoding.</param>
        /// <returns>Corrected string for Garmin device.</returns>
        private static string convertStringForGarmin(string text)
        {
            StringBuilder result = new StringBuilder ();

            // 
            foreach (char c in text)
            {
                if (c > 127)
                    result.Append(String.Format(@"\{0:000}", (int)c));
                else
                    result.Append(c);
            }
            // Replace linebreaks
            result.Replace (@"\n", @"<BR>\n");

            return result.ToString ();
        }

        /// <summary>
        /// Convert such strings, which we need for e.g. cartridge name and so on.
        /// </summary>
        /// <param name="text">Text, which should be converted.</param>
        /// <returns>Converted string.</returns>
        public string ConvertGWCString(string text)
        {
            StringBuilder result = new StringBuilder();

            foreach (byte b in Encoding.Convert(Encoding.Default, deviceEncoding[(int)device], Encoding.Default.GetBytes(text)))
                result.Append((char)b);

            // Do specials for the device
            if (deviceConvertGWCString[(int) device] != null)
                return deviceConvertGWCString[(int) device](result.ToString ());
            else
                return result.ToString ();
        }

        /// <summary>
        /// Convert special strings, which are needed for gwc file entries, for Garmin devices.
        /// </summary>
        /// <param name="text">String to convert to Garmin format.</param>
        /// <returns>Converted string.</returns>
        private static string convertGWCStringForGarmin(string text)
        {
            StringBuilder result = new StringBuilder();
            bool special = false;

            foreach (char c in text.ToCharArray ())
            {
                if (special)
                {
                    switch (c)
                    {
                        case 'a':
                            result.Append((char) 7);
                            break;
                        case 'b':
                            result.Append((char) 8);
                            break;
                        case 't':
                            result.Append((char) 9);
                            break;
                        case 'n':
                            result.Append("<BR>");
                            result.Append((char) 10);
                            break;
                        case 'v':
                            result.Append((char) 11);
                            break;
                        case 'f':
                            result.Append((char) 12);
                            break;
                        case 'r':
                            result.Append((char) 13);
                            break;
                        case '\"':
                            result.Append((char) 34);
                            break;
                        case '\'':
                            result.Append((char) 39);
                            break;
                        case '[':
                            result.Append((char) 91);
                            break;
                        case '\\':
                            result.Append((char) 92);
                            break;
                        case ']':
                            result.Append((char) 93);
                            break;
                        default:
                            result.Append('\\');
                            result.Append(c);
                            break;
                    }
                    special = false;
                }
                else
                {
                    if (c == 92)
                        special = true;
                    else
                        if (c==10)
                            result.Append(@"<BR>");
                        else
                            result.Append(c);
                }
            }

            return result.ToString();
        }

    }

}

