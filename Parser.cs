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
using System.Collections.Generic;
using System.Diagnostics;
using Ionic.Zip;
using SharpLua;
using SharpLua.Ast;
using SharpLua.Ast.Statement;
using SharpLua.Ast.Expression;

namespace WF.Compiler
{
	public class Parser
	{

        public string filenameGwz;
        private string filenameLua;
		private string filenameInfo;
		private List<String> files = new List<String> ();
		private List<String> stringList = new List<String> ();

		private Regex regexLuaLongString = new Regex ( @"\[(=*)\[(.*?)\]\1\]", RegexOptions.Singleline );

		public List<Error> Errors = new List<Error> ();

        public string FilenameGwz { get {return filenameGwz;} }

		/// <summary>
		/// Initializes a new instance of the <see cref="WF.Compiler.Parser"/> class.
		/// Checks also, if the gwz could be found, and if it contains a lua file
		/// </summary>
		/// <param name='filename'>
		/// Filename.
		/// </param>
		public Parser ( string filename )
		{
			// Check if fileName exists
			if ( !File.Exists ( filename ) )
			{
				// Try, if we could append a .gwz to the filename to get the file than
				if ( !File.Exists ( filename + ".gwz" ) )
					throw new FileNotFoundException ( "File not found", filename );
				else
					this.filenameGwz = filename + ".gwz";
			}
			else
			{
				// Save fileName
				this.filenameGwz = filename;
			}

			// Now open gwz file and read all relevant data
			ZipInputStream zipInput = new ZipInputStream ( File.OpenRead ( filename ) );

			ZipEntry zipEntry;

			while ( ( zipEntry = zipInput.GetNextEntry () ) != null ) 
			{
				switch ( Path.GetExtension ( zipEntry.FileName ).ToLower () )
				{
					case ".lua":
						filenameLua = zipEntry.FileName;
						break;
					case ".gwi":
						filenameInfo = zipEntry.FileName;
						break;
					default:
						files.Add ( zipEntry.FileName.ToLower () );
						break;
				}
			}

			zipInput.Close ();

			// Is gwz file a valid gwz file
			if ( filenameLua == null )
				throw new FileNotFoundException ( "Lua file not found", filename );
		}

		/// <summary>
		/// Checks the gwz file for:
		/// - Lua file
		/// - Info file
		/// - Lua code for right format
		/// - Media files for completeness.
		/// Errors could be found in the list Errors (with line and col, if known).
		/// </summary>
        /// <param name='zip'>
		/// Zip file for later use.
		/// </param>
		/// <returns>
		/// <c>true</c>, if the checked gwz was correct and complete, <c>false</c> otherwise.
		/// </returns>
		public bool CheckGWZ ( ZipFile zip, Cartridge cartridge )
		{
			// Get input stream for Lua file
			string luaCode = getLuaCode(zip,filenameGwz);
            
			// Now parse the Lua file
			Lexer lexer = new Lexer ();
			// Split the Lua code into tokens
			TokenReader tr = null;
			try
			{
				tr = lexer.Lex ( luaCode );
			}
			catch ( LuaSourceException e )
			{
				// There are something strange in the Lua file, so that the lexer couldn't create tokens
				Errors.Add ( new Error ( e.Line, e.Column, e.Message ) );
				return false;
			}
			// Jump to the first token
			tr.p = 0;
			// Save cartridge name for later use (the last is allways End of Stream token)
			cartridge.Variable = tr.tokens[tr.tokens.Count-2].Data;
			// Now parse the tokens, if it is all right with the Lua file
			SharpLua.Parser parser = new SharpLua.Parser ( tr );
			SharpLua.Ast.Chunk c = null;
			try
			{
				c = parser.Parse();
			}
			catch ( Exception e )
			{
				// Was there an error?
				if ( parser.Errors.Count > 0 )
				{
					foreach ( SharpLua.LuaSourceException error in parser.Errors )
						Errors.Add ( new Error ( error.Line, error.Column, error.Message ) );
					return false;
				}
			}
			// Now we are shure, that Lua code is allright, so we could go on
			// Create a dictionary, so we could get fast connection variable <-> media list entry
			Dictionary<String,Media> mediaDict = new Dictionary<String,Media> ();
			// Go through all statements and search cartridge info and all media definitions
			for ( int i = 0; i < c.Body.Count; i++ )
			{
				// We only check assignments in the parser result
				if ( c.Body[i] is AssignmentStatement )
				{
					AssignmentStatement statement = (AssignmentStatement) c.Body[i];
					// First get the left side ...
					Expression statementLeft = statement.Lhs[0];
					String left = getExpressionAsString ( statementLeft );
					// ... and than the right side
					Expression statementRight = statement.Rhs[0];
					String right = getExpressionAsString ( statementRight );
					// Is it an entry for the cartridge
					if ( left.Contains ( "." ) && cartridge.Variable.Equals ( left.Split ( '.' )[0] ) )
					{
						string key = left.Split ( '.' )[1];

						switch ( key )
						{
    						case "Id":
	    						cartridge.Id = right;
		    					break;
			    			case "Name":
				    			cartridge.Name = right;
					    		break;
						    case "Description":
    							cartridge.Description = removeLongString(right);
	    						break;
		    				case "Activity":
			    				cartridge.Activity = right;
				    			break;
					    	case "StartingLocationDescription":
						    	cartridge.StartingLocationDescription = removeLongString(right);
							    break;
    						case "StartingLocation":
                                if (statementRight is SharpLua.Ast.Expression.MemberExpr)
                                {
                                    // Play anywhere cartridge with starting location at 360.0 / 360.0 / 360.0
                                    cartridge.Latitude = 360.0;
                                    cartridge.Longitude = 360.0;
                                    cartridge.Altitude = 360.0;
                                }
                                else 
		    					    getZonePoints ( (SharpLua.Ast.Expression.CallExpr) statementRight, ref cartridge.Latitude, ref cartridge.Longitude, ref cartridge.Altitude );
			    				break;
				    		case "Version":
					    		cartridge.Version = right;
						    	break;
    						case "Company":
	    						cartridge.Company = right;
		    					break;
			    			case "Author":
				    			cartridge.Author = right;
					    		break;
						    case "BuilderVersion":
							    cartridge.BuilderVersion = right;
    							break;
	    					case "CreateDate":
		    					cartridge.CreateDate = right;
			    				break;
				    		case "PublishDate":
					    		cartridge.PublishDate = right;
						    	break;
    						case "UpdateDate":
	    						cartridge.UpdateDate = right;
		    					break;
			    			case "LastPlayedDate":
				    			cartridge.LastPlayedDate = right;
					    		break;
						    case "TargetDevice":
    							cartridge.TargetDevice = right;
	    						break;
		    				case "TargetDeviceVersion":
			    				cartridge.TargetDeviceVersion = right;
				    			break;
					    	case "StateId":
						    	cartridge.StateId = right;
							    break;
    						case "CountryId":
	    						cartridge.CountryId = right;
		    					break;
			    			case "Visible":
				    			cartridge.Visible = right.Equals ( "true" ) ? true : false;
					    		break;
						    case "Complete":
    							cartridge.Complete = right.Equals ( "true" ) ? true : false;
	    						break;
		    				case "UseLogging":
			    				cartridge.UseLogging = right.Equals ( "true" ) ? true : false;
				    			break;
					    	case "Media":
						    	cartridge.Splash = right;
							    break;
    						case "Icon":
	    						cartridge.Icon = right;
		    					break;
			    		}
				    }
					// Is it a ZMedia definition?
					if ( right.Equals ( "Wherigo.ZMedia" ) )
					{
						Media media = new Media ();
						media.Variable = left;
						cartridge.MediaList.Add ( media );
						mediaDict.Add ( left, media );
					}
					// Is it a ZMedia entry?
					if ( left.Contains ( "." ) &&  mediaDict.ContainsKey ( left.Split ( '.' )[0] ) )
					{
						// We found an entry to a ZMedia
						string key = left.Split ( '.' )[0];
						string value = left.Split ( '.' )[1];
						// Which key did we have found?
						switch ( value )
						{
							case "Name":
								mediaDict[key].Name = right;
								break;
							case "Description":
								mediaDict[key].Description = right;
								break;
							case "AltText":
								mediaDict[key].AltText = right;
								break;
							case "Id":
								mediaDict[key].Id = right;
								break;
							case "Resources":
								getMediaResources ( (SharpLua.Ast.Expression.TableConstructorExpr) statementRight, mediaDict[key].Resources );
								break;
						}
					}
				}
			}

			// Now check, if each media has a file in the gwz
			foreach ( Media m in cartridge.MediaList )
			{
				foreach ( MediaResource resource in m.Resources )
				{
					if ( !files.Contains ( resource.Filename.ToLower () ) )
					{
						// This filename couldn't be found in the gwz file
						Errors.Add ( new Error ( 0, 0, "Media file not found: " + resource.Filename ) );
					}
				}
			}

			// If there are no errors, than return true
			return Errors.Count == 0;
		}

        /// <summary>
        /// Replace strings with the device dependent ones.
        /// </summary>
        /// <param name="zip">ZipFile, which contains the Lua file.</param>
        /// <param name="player">Device, for which we would change the strings.</param>
        /// <returns>The updated Lua code.</returns>
		public string UpdateLua ( ZipFile zip, Player player)
		{
			// Get input stream for Lua file
			string luaCode = getLuaCode(zip,filenameGwz);

			// Now parse the Lua file
			Lexer lexer = new Lexer ();
			// Split the Lua code into tokens
			// This time we know, that the Lua file is ok, so we didn't ceare about errors
			TokenReader tr = null;
			tr = lexer.Lex ( luaCode );
			// Go to the beginning of the token stream
			tr.p = 0;

			// Now replace all strings with the right special character strings
			foreach ( Token t in tr.tokens )
				if ( t.Type == TokenType.DoubleQuoteString || t.Type == TokenType.LongString || t.Type == TokenType.SingleQuoteString )
					t.Data = player.ConvertString ( t.Data );

			// Now create the Lua file again
			StringBuilder result = new StringBuilder ( luaCode.Length );

            for (int i = 0; i < tr.tokens.Count - 3;i++ )
            {
                if (tr.tokens[i].Leading.Count > 0)
                    foreach (Token lt in tr.tokens[i].Leading)
                        result.Append(lt.Data);
                switch (tr.tokens[i].Type)
                {
                    case TokenType.DoubleQuoteString:
                        result.AppendFormat("\"{0}\"", tr.tokens[i].Data);
                        break;
                    case TokenType.LongString:
                        result.Append(tr.tokens[i].Data);
                        break;
                    case TokenType.SingleQuoteString:
                        result.AppendFormat("\'{0}\'", tr.tokens[i].Data);
                        break;
                    case TokenType.EndOfStream:
                        break;
                    default:
                        result.Append(tr.tokens[i].Data);
                        break;
                }
            }

            result.Append("\n\n");

            // If there are code, we should insert for this device, we do this now
            if (player.HasCode())
            {
                string code = player.GetCode(tr.tokens[tr.tokens.Count - 2].Data);
                if (code != "")
                    result.Append(code);
            }

            // If there is a library, we should insert, we do it now

            // Append "return cartridge" at the end
            result.Append("\n\n");
            result.Append(tr.tokens[tr.tokens.Count - 3].Data);
            result.Append(" ");
            result.Append(tr.tokens[tr.tokens.Count - 2].Data);

			return result.ToString ();
		}

        /// <summary>
        /// Extract the Lua file from gwz file, replace \ddd with byte code, replace special byte codes
        /// with Lua string codes. Than convert the whole bytes from UTF-8 to a UTF-16 string and replace 
        /// "<BR>\n" with "\n".
        /// </summary>
        /// <param name="zip">ZipFile, which contains the Lua file.</param>
        /// <param name="filename">Filename of the Lua file.</param>
        /// <returns>UTF-16 string with the whole Lua code.</returns>
        private string getLuaCode ( ZipFile zip, string filename)
        {
            BinaryReader br = new BinaryReader(zip[filenameLua].OpenReader());
            byte[] fileRawBytes = new byte[(int.MaxValue < zip[filenameLua].UncompressedSize ? int.MaxValue : (int)zip[filenameLua].UncompressedSize)];
            br.Read(fileRawBytes, 0, (int.MaxValue < fileRawBytes.Length ? int.MaxValue : (int)fileRawBytes.Length));

            // Convert codes like \226 to the corrosponding bytes
            byte[] fileBytes = new byte[fileRawBytes.Length];
            int pos = 0;

            for (int i = 0; i < fileRawBytes.Length - 3; i++)
            {
                if (fileRawBytes[i] == (byte)92 && isNumber(fileRawBytes[i + 1]) && isNumber(fileRawBytes[i + 2]) && isNumber(fileRawBytes[i + 3]))
                {
                    byte b = (byte)((fileRawBytes[i + 1] - 48) * 100);
                    b += (byte)((fileRawBytes[i + 2] - 48) * 10);
                    b += (byte)((fileRawBytes[i + 3] - 48));
                    // If this is a special character for Lua, than replace it
                    switch (b)
                    {
                        case 7: // BEL
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)'a';
                            break;
                        case 8: // BS
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)'b';
                            break;
                        case 9: // HT
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)'t';
                            break;
                        case 10: // LF
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)'n';
                            break;
                        case 11: // VT
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)'v';
                            break;
                        case 12: // FF
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)'f';
                            break;
                        case 13: // CR
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)'r';
                            break;
                        case 34: // "
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)34;
                            break;
                        case 39: // '
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)39;
                            break;
                        case 91: // [
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)91;
                            break;
                        case 92: // \
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)92;
                            break;
                        case 93: // ]
                            fileBytes[pos++] = (byte)92;
                            fileBytes[pos++] = (byte)93;
                            break;
                        default:
                            fileBytes[pos++] = b;
                            break;
                    }
                    i += 3;
                }
                else
                    fileBytes[pos++] = fileRawBytes[i];
            }

            // Copy the last 3 bytes, which are didn't checked
            fileBytes[pos++] = fileRawBytes[fileRawBytes.Length - 3];
            fileBytes[pos++] = fileRawBytes[fileRawBytes.Length - 2];
            fileBytes[pos++] = fileRawBytes[fileRawBytes.Length - 1];

            // Now encode bytes in UTF-8 format to a C# string
            string result = System.Text.Encoding.UTF8.GetString(fileBytes);

            // Replace special codes to there correct format
            result = result.Replace(@"<BR>\n", @"\n");

            return result;
        }

        /// <summary>
        /// Remove brackets ([=*[ and ]=*]) from long string.
        /// </summary>
        /// <param name="text">Long string with brackets.</param>
        /// <returns>Long string without brackets.</returns>
        private string removeLongString(string text)
        {
            string result = text;

            Match match = regexLuaLongString.Match(result);
            // If we match the regex for brackets, than save text between brackets
            if (match.Success)
                result = match.Groups[2].Value;

            return result;
        }

        /// <summary>
        /// Check, if b is a digit (ascii code is between 48 '0' and 57 '9').
        /// </summary>
        /// <param name="b">Byte, which should be checked.</param>
        /// <returns>True, if b is a ascii digit, else false.</returns>
        private bool isNumber(byte b)
        {
            return (b >= (byte) 48 && b <= (byte) 57);
        }

        /// <summary>
        /// Extract strings from AST chunk.
        /// </summary>
        /// <param name="expr">Expression, which is found in the chunk.</param>
        /// <returns>String, which is in the expression.</returns>
        private string getExpressionAsString(Expression expr)
		{
			string result = "";

            if (expr is VariableExpression)
			{
				result = ( (VariableExpression) expr ).Var.Name;
			}
			if ( expr is BoolExpr )
			{
				result = ( (BoolExpr) expr ).Value ? "true" : "false";
			}
			if ( expr is CallExpr && ( (CallExpr) expr ).Base is MemberExpr )
			{
				expr = ( (CallExpr) expr ).Base;
			}
            // Inserted this, because Earwigo used WWB_multiplatform_string. If this is gone, we could remove this
            if (expr is CallExpr && ((CallExpr)expr).Arguments[0] is StringExpr)
            {
                expr = ((CallExpr)expr).Arguments[0];
            }
            if (expr is MemberExpr && ((MemberExpr)expr).Base is VariableExpression)
			{
				result = getExpressionAsString ( ( (MemberExpr) expr ).Base ) + "." + ( (MemberExpr) expr ).Ident;
			}
			if ( expr is StringExpr )
			{
				StringExpr str = (StringExpr) expr;
				result = str.Value;
			}

			return result;
		}

        /// <summary>
        /// Extract table entries for the Resources entry of a media.
        /// </summary>
        /// <param name="expr">Expression, which is found in the chunk.</param>
        /// <param name="resources">List for all media resources of this ZMedia.</param>
		private void getMediaResources ( TableConstructorExpr expr, List<MediaResource> resources )
		{
			foreach ( TableConstructorValueExpr entry in expr.EntryList )
			{
				resources.Add ( getMediaResource ( (TableConstructorExpr) entry.Value ) );
			}
		}

        /// <summary>
        /// Extract table entries for this resource entry of resources.
        /// </summary>
        /// <param name="expr">Expression, which is found in the chunk.</param>
        /// <returns>A media resource object with type, filename and directives. </returns>
		private MediaResource getMediaResource ( TableConstructorExpr expr )
		{
			MediaResource result = new MediaResource ();

			foreach ( TableConstructorStringKeyExpr entry in expr.EntryList )
			{
				switch ( entry.Key )
				{
				case "Type":
					result.Type = (MediaFormat) Enum.Parse ( typeof ( MediaFormat ), getExpressionAsString ( entry.Value ), true );
					break;
				case "Filename":
					result.Filename = getExpressionAsString ( entry.Value );
					break;
				case "Directives":
					foreach ( StringExpr str in ( (TableConstructorExpr) entry.Value ).EntryList )
						result.Directives.Add ( str.Value );
					break;

				}
			}

			return result;
		}

        /// <summary>
        /// Extract zone points from a ZonePoint(lat,lon,alt) expression. 
        /// </summary>
        /// <param name="expr">Expression, which is found in the chunk.</param>
        /// <param name="lat">Retrived latitude.</param>
        /// <param name="lon">Retrived longitude.</param>
        /// <param name="alt">Retrived altitude.</param>
        private void getZonePoints ( SharpLua.Ast.Expression.CallExpr expr, ref double lat, ref double lon, ref double alt )
        {
            if (expr.Arguments[0] is SharpLua.Ast.Expression.UnOpExpr)
                lat = -double.Parse(((SharpLua.Ast.Expression.NumberExpr)((SharpLua.Ast.Expression.UnOpExpr)expr.Arguments[0]).Rhs).Value, System.Globalization.CultureInfo.InvariantCulture);
            else
                lat = double.Parse(((SharpLua.Ast.Expression.NumberExpr)expr.Arguments[0]).Value, System.Globalization.CultureInfo.InvariantCulture);
            if (expr.Arguments[1] is SharpLua.Ast.Expression.UnOpExpr)
                lon = -double.Parse(((SharpLua.Ast.Expression.NumberExpr)((SharpLua.Ast.Expression.UnOpExpr)expr.Arguments[1]).Rhs).Value, System.Globalization.CultureInfo.InvariantCulture);
            else
                lon = double.Parse(((SharpLua.Ast.Expression.NumberExpr)expr.Arguments[1]).Value, System.Globalization.CultureInfo.InvariantCulture);
            if (expr.Arguments[2] is SharpLua.Ast.Expression.UnOpExpr)
                alt = -double.Parse(((SharpLua.Ast.Expression.NumberExpr)((SharpLua.Ast.Expression.UnOpExpr)expr.Arguments[2]).Rhs).Value, System.Globalization.CultureInfo.InvariantCulture);
            else
                alt = double.Parse(((SharpLua.Ast.Expression.NumberExpr)expr.Arguments[2]).Value, System.Globalization.CultureInfo.InvariantCulture);
        }


	}

	public class Error
	{
		public int Line;
		public int Column;
		public string Message;

		public Error ()
		{
		}

		public Error ( int line, int col, string message )
		{
			Line = line;
			Column = col;
			Message = message;
		}

	}

}
