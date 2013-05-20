using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wherigo.UI.BLL
{
	public class GWZParser //: IDisposable
	{
		public string filePath { get; private set; }
		public App_Code.Cartridge lastParse { get; private set; }

		internal GWZParser(string path)
		{
			filePath = path;
		}

		//public void Dispose() { if (System.IO.File.Exists(filePath)) try { System.IO.File.Delete(filePath); } catch {} }
		/*~GWZParser() 
		{ 
			if (System.IO.File.Exists(filePath)) try { System.IO.File.Delete(filePath); } catch {}
			if (lastParse != null && lastParse.isPosterFileNameDirty) try { System.IO.File.Delete(lastParse.PosterFileName); } catch { }
		}*/

		public App_Code.Cartridge Parse(out string error)
		{
			error = null;
			lastParse = null;
			App_Code.Cartridge info = new App_Code.Cartridge();
			info.SetResource(Data.Cartridge.ResourceType.GWZ, filePath);

			//Does the file even exist?
			if (System.IO.File.Exists(error)) { error = "Could not find the file."; return null; }


			string IconName=null, IconFileName=null;
			//Unzip the file
			using(Ionic.Zip.ZipFile zip = Ionic.Zip.ZipFile.Read(filePath))
			{
				foreach(Ionic.Zip.ZipEntry e in zip)
				{
					if (!e.FileName.ToLower().EndsWith(".lua")) continue;

					//We have the lua file.  Look through it.
					string luatext = null;
					using (Ionic.Crc.CrcCalculatorStream fs = e.OpenReader())
					{
						byte[] fileBytes = new byte[(int.MaxValue < fs.Length ? int.MaxValue : (int)fs.Length)];
						fs.Read(fileBytes, 0, (int.MaxValue < fs.Length ? int.MaxValue : (int)fs.Length));

						luatext = System.Text.Encoding.Default.GetString(fileBytes);
					}

					//Now, we do a lot of string searches:
					
					if (!StoreOrError(luatext, "Wherigo.ZCartridge()", true, ref info.internalCartObjectID))
					{
						//This is fatal.  Stop doing everything.
						error = "Could not find the cartridge object ID."; return null;
					}

					string str = null;
					if (StoreOrError(luatext, info.internalCartObjectID + ".Id", false, ref str))
					{
						System.Guid g;
						if (System.Guid.TryParse(str, out g)) info.luaGuid = g;
					}

					StoreOrError(luatext, info.internalCartObjectID + ".Name", false, ref info.Name);
					StoreOrError(luatext, info.internalCartObjectID + ".Description", false, ref info.Description);
					/*if (StoreOrError(luatext, info.internalCartObjectID + ".Visible", false, ref str))
					{
						bool b;
						if (bool.TryParse(str, out b)) info.Visible = b;
					}*/
					if (StoreOrError(luatext, info.internalCartObjectID + ".Activity", false, ref str))
					{
						str = str.ToUpper();
						foreach (Data.Cartridge.Activities activity in Enum.GetValues(typeof(Data.Cartridge.Activities)))
						{
							if (activity.ToString().ToUpper().Equals(str))
							{
								info.Activity = activity;
								break;
							}
						}
					}
					StoreOrError(luatext, info.internalCartObjectID + ".StartingLocationDescription", false, ref info.StartingLocationDescription);
					if (StoreOrError(luatext, info.internalCartObjectID + ".StartingLocation", false, ref str))
					{
						Classes.ZonePoint zp = new Classes.ZonePoint(str);
						if ((zp.latitude >= -90 && !zp.isPlayAnywhere) || zp.isPlayAnywhere) info.StartingLocation = zp;
						else { error = "Could not find the cartridge's StartingLocation variable."; return null; }
					}
					else { error = "Could not find the cartridge's StartingLocation variable."; return null; }

					StoreOrError(luatext, info.internalCartObjectID + ".Version",false, ref info.Version);
					StoreOrError(luatext, info.internalCartObjectID + ".Company", false, ref info.Company);
					StoreOrError(luatext, info.internalCartObjectID + ".Author", false, ref info.Author);
					StoreOrError(luatext, info.internalCartObjectID + ".BuilderVersion", false, ref info.BuilderVersion);
					if (StoreOrError(luatext, info.internalCartObjectID + ".CreateDate", false, ref str))
					{
						System.DateTime dt;
						if (System.DateTime.TryParse(str, out dt)) info.CreateDate = dt;
					}
					if (StoreOrError(luatext, info.internalCartObjectID + ".PublishDate", false, ref str))
					{
						System.DateTime dt;
						if (System.DateTime.TryParse(str, out dt)) info.PublishDate = dt;
					}
					if (StoreOrError(luatext, info.internalCartObjectID + ".UpdateDate", false, ref str))
					{
						System.DateTime dt;
						if (System.DateTime.TryParse(str, out dt)) info.UpdateDate = dt;
					}
					if (StoreOrError(luatext, info.internalCartObjectID + ".LastPlayedDate", false, ref str))
					{
						System.DateTime dt;
						if (System.DateTime.TryParse(str, out dt)) info.LastPlayedDate = dt;
					}
					StoreOrError(luatext, info.internalCartObjectID + ".TargetDevice", false, ref info.TargetDevice);
					StoreOrError(luatext, info.internalCartObjectID + ".TargetDeviceVersion", false, ref info.TargetDeviceVersion);
					if (StoreOrError(luatext, info.internalCartObjectID + ".StateId", false, ref str))
					{
						short i; if (short.TryParse(str, out i)) info.StateID = i;
					}
					if (StoreOrError(luatext, info.internalCartObjectID + ".CountryId", false, ref str))
					{
						short i; if (short.TryParse(str, out i)) info.CountryID = i;
					}
					if (StoreOrError(luatext, info.internalCartObjectID + ".UseLogging", false, ref str))
					{
						bool b; if (bool.TryParse(str, out b)) info.UseLogging = b;
					}
					StoreOrError(luatext, info.internalCartObjectID + ".Icon", false, ref IconName);

					if (!String.IsNullOrWhiteSpace(IconName))
					{
						IconFileName = FindMediaFileName(luatext, IconName);
					}

					
					//All done
					break;
				}


				if (String.IsNullOrWhiteSpace(info.Name))
				{
					error = "A lua file was not detected in the uploaded cartridge.";
					return null;
				}


				//Find the cartridge's icon (or poster)
				if (!String.IsNullOrWhiteSpace(IconFileName))
				{
					//Do we REALLY have to do all that AGAIN?
					foreach (Ionic.Zip.ZipEntry e in zip)
					{
						if (!e.FileName.ToLower().EndsWith(IconFileName.ToLower())) continue;

						string fname = AppDomain.CurrentDomain.BaseDirectory + "/upload_files/" + System.Guid.NewGuid().ToString() + 
							System.IO.Path.GetExtension(e.FileName);

						using (System.IO.FileStream fs = new System.IO.FileStream(fname, System.IO.FileMode.CreateNew))
							e.Extract(fs);

						info.SetResource(Data.Cartridge.ResourceType.Poster, fname);



						//Make the icon
						string iconfname = AppDomain.CurrentDomain.BaseDirectory + "/upload_files/" + System.Guid.NewGuid().ToString() +
								System.IO.Path.GetExtension(e.FileName);

						try
						{
							System.IO.File.Copy(fname, iconfname);
							Utils.ResizeToMaxDimensions(iconfname, Utils.MaximumIconWidth, Utils.MaximumIconHeight);
							info.SetResource(Data.Cartridge.ResourceType.Icon, iconfname);
						}
						catch (Exception ex)
						{
							try { System.IO.File.Delete(iconfname); }
							catch { }
						}


						break;
					}
				}


				//All done.
				lastParse = info;
				return info;
			}

			return null;
		}

		private bool StoreOrError(string lua, string search, bool beforeEquals, ref string storeIn)
		{
			string str = GetLine(lua, search, !beforeEquals);
			if (str == null) return false;

			str = beforeEquals ? BeforeEqualsSign(str) : AfterEqualsSign(str);
			if (str == null) return false;

			storeIn = str;
			return true;
		}

		private string GetLine(string lua, string search, bool enforceDuplicationCheck)
		{
			int index = lua.IndexOf(search);
			if (index == -1) return null;

			if (enforceDuplicationCheck)
			{
				do
				{
					//Make sure it's the right one (followed by either a space or equals sign).
					//	We do this because StartingLocation could match StartingLocationDescription otherwise.
					string sub = lua.Substring(index + search.Length, 1);
					if (sub.Equals(" ") || sub.Equals("=")) break;
					index = lua.IndexOf(search, index + 1);
				} while (index != -1);

				if (index == -1) return null;
			}

			int lastLineBreak = lua.IndexOf("\n", index, StringComparison.InvariantCultureIgnoreCase);
			int temp = lua.IndexOf("\r", index, StringComparison.InvariantCultureIgnoreCase);
			if (temp < lastLineBreak) lastLineBreak = temp;

			int startLineBreak = index;
			for (; startLineBreak > 0; startLineBreak--)
			{
				string str = lua.Substring(startLineBreak, 1);
				if (str.Equals("\n") || str.Equals("\r")) break; 
			}

			string substr = lua.Substring(startLineBreak + 1, lastLineBreak - startLineBreak - 1);
			if (!substr.Contains("[[")) return substr;

			//Looks like we'll have to look for the ending ]] instead.
			int textTerminatorIndex = lua.IndexOf("]]", startLineBreak);
			return lua.Substring(startLineBreak + 1, textTerminatorIndex - startLineBreak + 1);
		}

		private string BeforeEqualsSign(string str)
		{
			if (!str.Contains("=")) return null;
			return str.Substring(0, str.IndexOf("=")).Trim();
		}

		private string AfterEqualsSign(string str)
		{
			if (!str.Contains("=")) return null;
			int idx = str.IndexOf("=");
			str = str.Substring(idx + 1).Trim();

			if (str.StartsWith("\""))
				str = str.Substring(1, str.Length - 2);
			else if (str.StartsWith("[["))
				str = str.Substring(2, str.Length - 4);

			return str;
		}

		private string FindMediaFileName(string lua, string zmedia)
		{
			int idx = lua.IndexOf(zmedia + ".Resources",  StringComparison.InvariantCultureIgnoreCase);
			if (idx == -1) return null;

			idx = lua.IndexOf("Filename", idx,  StringComparison.InvariantCultureIgnoreCase);
			if (idx == -1) return null;

			string str = AfterEqualsSign(lua.Substring(idx, lua.IndexOf(",", idx)));
			if (str == null) return null;

			str = str.Substring(0, str.IndexOf("\""));
			str = str.Replace("\"", "").Replace(",", "");
			return str;
		}
	}

	
}