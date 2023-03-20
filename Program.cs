using System;
using System.IO;
using System.Linq;

namespace TouchMax;

/*
	* Usage:
	*		TouchMax +/-HH:MM <file specification>
	*
	* Pass a file path (with optional wildcards) and the offset to use to
	* change the file's last-modified time.
	*/
class Program
{
	private string _strDirectory = ".";
	private string _strPattern = string.Empty;

	private bool _bSetFiles = false;
	private bool _bSetFolders = false;
	private bool _bRecurse = false;

	private bool _bSetCreation = false;
	private bool _bSetModified = false;

	// use these for setting relative components
	private int _relYears = 0;
	private int _relMonths = 0;
	// could combine these three into a TimeSpan, but why break the parallels
	private int _relDays = 0;
	private int _relHours = 0;
	private int _relMinutes = 0;

	// use these for setting absolute components
	private int? _absYear = null;
	private int? _absMonth = null;
	private int? _absDate = null;
	private int? _absHour = null;
	private int? _absMinute = null;

	// use this for setting "now"
	private bool _bUseCreation = false;
	private bool _bUseModified = false;
	private bool _bUseNow = false;
	private readonly DateTime _dtNow = DateTime.Now;

	private delegate bool DirectoryInfoHandler(DirectoryInfo dirinfo);
	private delegate bool FileInfoHandler(FileInfo dirinfo);


	static void Main(string[] args)
	{
		if (args.Length < 4)
		{
			//Console.WriteLine("Command line: " + String.Join(";", args));
			ShowUsage();
			return;
		}

		Program pgm = new();
		try
		{
			pgm.ParseArguments(args);
		}
		catch (Exception)
		{
			return;
		}

		pgm.Touch(pgm._strDirectory, pgm._strPattern);
	}


	private Program() { }

	private void Touch(string strPath, string strPattern)
	{
		ProcessSubdirectoriesAndFiles(nLevel: 0, OffsetDirectory, OffsetFile, strPath, strPattern);
	}


	/// <summary>
	/// This function processes every subdirectory and file in the passed
	/// directory that matches the passed pattern.
	/// For each file, it calls the passed file handler delegate (if it's not null).
	/// For each directory, it calls the passed directory handler delegate (if it's not null).
	/// If a delegate returns false, it stops processing the files/dirs.
	/// If it returns true, it continues.
	/// </summary>
	/// <param name="nLevel">Level of recursion</param>
	/// <param name="handlerDirInfo">Delegate to process a folder.</param>
	/// <param name="handlerFileInfo">Delegate to process a file.</param>
	/// <param name="strPath"></param>
	/// <param name="strPattern"></param>
	/// <returns>True when the recursion is finished. False to continue.</returns>
	private bool ProcessSubdirectoriesAndFiles(int nLevel,
																DirectoryInfoHandler handlerDirInfo,
																FileInfoHandler handlerFileInfo,
																string strPath,
																string strPattern)
	{
		// can throw System.ArgumentException
		DirectoryInfo dir = new(strPath);
		if (!dir.Exists)
		{
			Console.WriteLine($"The directory {strPath} does not exist.");
			return true;
		}

		Console.WriteLine();
		string strIndent = Path.AltDirectorySeparatorChar.ToString().PadLeft(1 + nLevel, '\t');
		Console.WriteLine(strIndent + dir.Name + Path.AltDirectorySeparatorChar);

		/*
		 * Since we're using a pattern, the subdirectories might not (probably
		 * won't) match it. So, we process any files that match the pattern.
		 * Then, separately, we process all subdirectories.
		 *
		 * Alternative: FileSystemInfo fsinfos = dir.GetFileSystemInfos(strPattern);
		 */

		/*
		 * Process each file
		 * If any return false, we quit.
		 */
		if (_bSetFiles && (handlerFileInfo is not null))
		{
			try
			{
				if (dir.GetFiles(strPattern).Any(fileinfo => !handlerFileInfo(fileinfo)))
				{
					return false;
				}
			}
			catch (DirectoryNotFoundException ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		/*
		 * Process each folder
		 * If any return false, we quit.
		 */
		if (_bSetFolders && (handlerDirInfo is not null))
		{
			try
			{
				if (dir.GetDirectories(strPattern).Any(dirinfo => !handlerDirInfo(dirinfo)))
				{
					return false;
				}
			}
			catch (DirectoryNotFoundException ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		/*
		 * Recurse into all subfolders
		 */
		if (_bRecurse)
		{
			foreach (DirectoryInfo dirinfo in dir.GetDirectories())
			{
				ProcessSubdirectoriesAndFiles(nLevel + 1, handlerDirInfo!, handlerFileInfo!, dirinfo.FullName, strPattern);
			}
		}

		return true;
	}

	private bool OffsetFile(FileInfo fileinfo)
	{
		Console.WriteLine(fileinfo.Name);

		DateTime dtCreation = File.GetCreationTime(fileinfo.FullName);
		DateTime dtModified = File.GetLastWriteTime(fileinfo.FullName);

		if (_bSetCreation)
		{
			DateTime dtNew = DetermineNewDateTime(dtCreation, dtCreation, dtModified);

			try
			{
				File.SetCreationTime(fileinfo.FullName, dtNew);
				Console.WriteLine("\tC: {0}\t{1}", dtCreation.ToString(), File.GetCreationTime(fileinfo.FullName).ToString());
			}
			catch (IOException ex)
			{
				Console.WriteLine($"Unable to set Creation-Time of {fileinfo.Name} to {dtNew}.");
				Console.WriteLine(ex.Message);
			}
		}

		if (_bSetModified)
		{
			DateTime dtNew = DetermineNewDateTime(dtModified, dtCreation, dtModified);

			try
			{
				File.SetLastWriteTime(fileinfo.FullName, dtNew);
				Console.WriteLine("\tM: {0}\t{1}", dtModified.ToString(), File.GetLastWriteTime(fileinfo.FullName).ToString());
			}
			catch (IOException ex)
			{
				Console.WriteLine($"Unable to set Modified-Time of {fileinfo.Name} to {dtNew}.");
				Console.WriteLine(ex.Message);
			}
		}

		return true;
	}

	private bool OffsetDirectory(DirectoryInfo dirinfo)
	{
		Console.WriteLine(dirinfo.Name);

		DateTime dtCreation = Directory.GetCreationTime(dirinfo.FullName);
		DateTime dtModified = Directory.GetLastWriteTime(dirinfo.FullName);
		if (_bSetCreation)
		{
			DateTime dtNew = DetermineNewDateTime(dtCreation, dtCreation, dtModified);

			try
			{
				Directory.SetCreationTime(dirinfo.FullName, dtNew);
				Console.WriteLine("\tC: {0}\t{1}", dtCreation.ToString(), Directory.GetCreationTime(dirinfo.FullName).ToString());
			}
			catch (IOException ex)
			{
				Console.WriteLine($"Unable to set Creation-Time of {dirinfo.Name} to {dtNew}.");
				Console.WriteLine(ex.Message);
			}
		}

		if (_bSetModified)
		{
			DateTime dtNew = DetermineNewDateTime(dtModified, dtCreation, dtModified);

			try
			{
				Directory.SetLastWriteTime(dirinfo.FullName, dtNew);
				Console.WriteLine("\tM: {0}\t{1}", dtModified.ToString(), Directory.GetLastWriteTime(dirinfo.FullName).ToString());
			}
			catch (IOException ex)
			{
				Console.WriteLine($"Unable to set Modified-Time of {dirinfo.Name} to {dtNew}.");
				Console.WriteLine(ex.Message);
			}
		}

		return true;
	}

	private DateTime DetermineNewDateTime(DateTime dt, DateTime dtCreation, DateTime dtModified)
	{
		/*
		 * First, determine if we should start with the file's date/time
		 * or if we should start with the current time.
		 */
		DateTime dtNew = dt;
		if (_bUseNow)
		{
			dtNew = _dtNow;
		}
		else if (_bUseCreation)
		{
			dtNew = dtCreation;
		}
		else if (_bUseModified)
		{
			dtNew = dtModified;
		}

		/*
		 * Next, apply any absolute settings that were specified
		 */
		try
		{
			if (_absYear.HasValue)
			{
				dtNew = new DateTime(_absYear.Value, dtNew.Month, dtNew.Day, dtNew.Hour, dtNew.Minute, dtNew.Second);
			}
			if (_absMonth.HasValue)
			{
				dtNew = new DateTime(dtNew.Year, _absMonth.Value, dtNew.Day, dtNew.Hour, dtNew.Minute, dtNew.Second);
			}
			if (_absDate.HasValue)
			{
				dtNew = new DateTime(dtNew.Year, dtNew.Month, _absDate.Value, dtNew.Hour, dtNew.Minute, dtNew.Second);
			}
			if (_absHour.HasValue)
			{
				dtNew = new DateTime(dtNew.Year, dtNew.Month, dtNew.Day, _absHour.Value, dtNew.Minute, dtNew.Second);
			}
			if (_absMinute.HasValue)
			{
				dtNew = new DateTime(dtNew.Year, dtNew.Month, dtNew.Day, dtNew.Hour, _absMinute.Value, dtNew.Second);
			}
		}
		catch (ArgumentOutOfRangeException ex)
		{
			Console.WriteLine(ex.Message);
			throw;
		}

		/*
		 * Finally, apply any specified relative changes.
		 */
		dtNew = dtNew.AddYears(_relYears);
		dtNew = dtNew.AddMonths(_relMonths);
		dtNew = dtNew.AddDays(_relDays);
		dtNew = dtNew.AddHours(_relHours);
		dtNew = dtNew.AddMinutes(_relMinutes);

		return dtNew;
	}


	private void ParseArguments(string[] args)
	{
		foreach (string a in args)
		{
			try
			{
				if (a.StartsWith("-") || a.StartsWith("/"))
				{
					string key = a[1..].ToLower();
					if (key.StartsWith("recurse"))
					{
						_bRecurse = true;
						continue;
					}
					else if (key.StartsWith("setfiles"))
					{
						_bSetFiles = true;
						continue;
					}
					else if (key.StartsWith("setfolders"))
					{
						_bSetFolders = true;
						continue;
					}
					else if (key.StartsWith("setcreation"))
					{
						_bSetCreation = true;
						continue;
					}
					else if (key.StartsWith("setmodified"))
					{
						_bSetModified = true;
						continue;
					}
					else if (key.StartsWith("usenow"))
					{
						_bUseNow = true;
						continue;
					}
					else if (key.StartsWith("usecreation"))
					{
						_bUseCreation = true;
						continue;
					}
					else if (key.StartsWith("usemodified"))
					{
						_bUseModified = true;
						continue;
					}

					switch (a[1])
					{
						case 'Y':
						{
							int n = ParseArgumentNumber(a);
							if (a[2] == '=')
							{
								_absYear = n;
							}
							else
							{
								_relYears = n;
							}
							break;
						}
						case 'M':
						{
							int n = ParseArgumentNumber(a);
							if (a[2] == '=')
							{
								_absMonth = n;
							}
							else
							{
								_relMonths = n;
							}
							break;
						}
						case 'D':
						{
							int n = ParseArgumentNumber(a);
							if (a[2] == '=')
							{
								_absDate = n;
							}
							else
							{
								_relDays = n;
							}
							break;
						}

						case 'h':
						{
							int n = ParseArgumentNumber(a);
							if (a[2] == '=')
							{
								_absHour = n;
							}
							else
							{
								_relHours = n;
							}
							break;
						}
						case 'm':
						{
							int n = ParseArgumentNumber(a);
							if (a[2] == '=')
							{
								_absMinute = n;
							}
							else
							{
								_relMinutes = n;
							}
							break;
						}

						default:
							Console.WriteLine($"Unrecognized switch: {a}");
							throw new ArgumentException("Unrecognized switch", a);
					}

					continue;
				}

				// maybe it's the pattern
				if (string.IsNullOrEmpty(_strPattern))
				{
					_strPattern = a;
					/*
					 * The user may have specified a path (absolute or relative) and
					 * we need to split them up.
					 */
					_strDirectory = Path.GetDirectoryName(_strPattern) ?? ".";
					_strPattern = Path.GetFileName(_strPattern);
					if (string.IsNullOrEmpty(_strDirectory))
					{
						_strDirectory = ".";
					}
				}
				else
				{
					Console.WriteLine($"Too many arguments: {a}");
				}
			}
			catch (ArgumentOutOfRangeException /*ex*/)
			{
				Console.WriteLine($"Invalid argument value: {a}");
				throw;
			}
		}

		/*
		Console.WriteLine("Recurse? " + (_bRecurse ? "YES" : "NO"));
		Console.WriteLine("Set files? " + (_bSetFiles ? "YES" : "NO"));
		Console.WriteLine("Set folders? " + (_bSetFolders ? "YES" : "NO"));
		Console.WriteLine("Now: " + _dtNow.ToString());
		Console.WriteLine("Base: Use " + (_bUseNow ? "NOW" : _bUseCreation ? "CREATION" : "MODIFIED"));
		Console.WriteLine("Absolute:");
		Console.WriteLine("\tyear = " + _absYear);
		Console.WriteLine("\tmonth = " + _absMonth);
		Console.WriteLine("\tdate = " + _absDate);
		Console.WriteLine("\thour = " + _absHour);
		Console.WriteLine("\tminute = " + _absMinute);
		Console.WriteLine("Relative:");
		Console.WriteLine("\tyears = " + _relYears);
		Console.WriteLine("\tmonths = " + _relMonths);
		Console.WriteLine("\tdays = " + _relDays);
		Console.WriteLine("\thour = " + _relHours);
		Console.WriteLine("\tminute = " + _relMinutes);
		Console.WriteLine("Directory: " + _strDirectory);
		Console.WriteLine("Pattern: " + _strPattern);
		*/
		if (!_bSetFiles && !_bSetFolders)
		{
			Console.WriteLine("You must set file or folder timestamps.");
			Environment.Exit(-1);
		}

		if (!_bSetCreation && !_bSetModified)
		{
			Console.WriteLine("You must set creation or modified timestamp.");
			Environment.Exit(-1);
		}

		static int ParseArgumentNumber(string s)
		{
			try
			{
				// + or - or =
				switch (s[2])
				{
					case '-': return -short.Parse(s[3..]);
					case '+': return short.Parse(s[3..]);
					case '=': return short.Parse(s[3..]);
					default:
						Console.WriteLine($"Must use +, -, or = after switch {s}");
						break;
				}
			}
			catch (FormatException ex)
			{
				Console.WriteLine(ex.Message);
				throw;
			}
			return 0;
		}
	}


	static void ShowUsage()
	{
		System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
		var exeName = assembly.GetName().Name;
		/// assembly.Location is empty for single-file package, so we use this.
		System.Diagnostics.FileVersionInfo versionFile = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(AppContext.BaseDirectory, $"{exeName}.exe"));

		Console.WriteLine();
		Console.WriteLine("{0} {1} {2}", versionFile.CompanyName ?? string.Empty, versionFile.ProductName ?? string.Empty, versionFile.ProductVersion ?? string.Empty);
		Console.WriteLine("{0}", versionFile.LegalCopyright ?? string.Empty);
		Console.WriteLine("12noon.com");
		Console.WriteLine();
		Console.WriteLine("USAGE");
		Console.WriteLine("   [/setfiles] [/setfolders] [/recurse] [/setcreation] [/setmodified] [/usenow|/usecreation|/usemodified]");
		Console.WriteLine("   [/Y+|-|=#] [/M+|-|=#] [/D+|-|=#] [/h+|-|=#] [/m+|-|=#] <pattern>");
		Console.WriteLine("\t/setfiles: set file timestamps");
		Console.WriteLine("\t/setfolders: set folder timestamps");
		Console.WriteLine("\t/recurse: process subfolders");
		Console.WriteLine("\t/setcreation: change creation time");
		Console.WriteLine("\t/setmodified: change modified time");
		Console.WriteLine("\t/usenow: set timestamp to current time first");
		Console.WriteLine("\t/usecreation: set timestamp to file's creation time first");
		Console.WriteLine("\t/usemodified: set timestamp to file's modified time first");
		Console.WriteLine("\t/Y - year, /M - month, /D - date, /h - hour, /m - minute");
		Console.WriteLine("\t\t(January is 1, etc. Dates are 1-31.)");
		Console.WriteLine("\t\t+ increments the current value by the specified amount");
		Console.WriteLine("\t\t- decrements the current value by the specified amount");
		Console.WriteLine("\t\t= sets to the specified value");
		Console.WriteLine();
		Console.WriteLine("PROCESS");
		Console.WriteLine("These are the steps it takes to set a file or folder's timestamp:");
		Console.WriteLine("\t1. Set timestamp to now, if specified");
		Console.WriteLine("\t2. Apply absolute values, if any");
		Console.WriteLine("\t3. Apply relative changes, if any");
		Console.WriteLine();
		Console.WriteLine("EXAMPLES");
		Console.WriteLine("Forgot to set camera ahead one hour for Daylight Saving Time. Set Creation-Time of all JPG files in all folders ahead one hour:");
		Console.WriteLine("\ttouchmax.exe /recurse /setfiles /setcreation /h+1 *.jpg");
		Console.WriteLine();
		Console.WriteLine("Set Modified-Time of text files to a month ago and ten minutes ahead:");
		Console.WriteLine("\ttouchmax.exe /setfiles /setmodified /usenow /M-1 /m+10 *.txt");
		Console.WriteLine();
		Console.WriteLine("Set Creation-Time to three days before the ModifiedTime:");
		Console.WriteLine("\ttouchmax.exe /setfiles /setmodified /usemodified /D-3 *.txt");
		Console.WriteLine();
		Console.WriteLine("Set a file's Modified-Time to 15 Sep 2008:");
		Console.WriteLine("\ttouchmax.exe /setfiles /setmodified /usemodified /Y=2008 /M=9 /D=15 test.txt");
	}
}
