using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;

namespace TouchMax;

internal class CustomHelpAction(HelpAction _defaultAction) : SynchronousCommandLineAction
{
	public override int Invoke(ParseResult parseResult)
	{
		// Output version and copyright information
		System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
		string? exeName = assembly.GetName().Name;
		System.Diagnostics.FileVersionInfo versionFile = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(AppContext.BaseDirectory, $"{exeName}.exe"));

		parseResult.InvocationConfiguration.Output.WriteLine();
		parseResult.InvocationConfiguration.Output.WriteLine($"{versionFile.CompanyName ?? string.Empty} {versionFile.ProductName ?? string.Empty} {versionFile.ProductVersion ?? string.Empty}");
		parseResult.InvocationConfiguration.Output.WriteLine($"{versionFile.LegalCopyright ?? string.Empty}");
		parseResult.InvocationConfiguration.Output.WriteLine("12noon.com");
		parseResult.InvocationConfiguration.Output.WriteLine();

		// Output default help
		int result = _defaultAction.Invoke(parseResult);

		// Output additional examples
		parseResult.InvocationConfiguration.Output.WriteLine();
		parseResult.InvocationConfiguration.Output.WriteLine("PROCESS");
		parseResult.InvocationConfiguration.Output.WriteLine("These are the steps the app takes to set a file or folder's timestamp:");
		parseResult.InvocationConfiguration.Output.WriteLine("  1. Set timestamp to now, if specified");
		parseResult.InvocationConfiguration.Output.WriteLine("  2. Apply absolute values, if any");
		parseResult.InvocationConfiguration.Output.WriteLine("  3. Apply relative changes, if any");
		parseResult.InvocationConfiguration.Output.WriteLine();
		parseResult.InvocationConfiguration.Output.WriteLine("EXAMPLES");
		parseResult.InvocationConfiguration.Output.WriteLine("  Set Modified-Time of a file to the current date/time:");
		parseResult.InvocationConfiguration.Output.WriteLine("    touchmax --set-files --set-modified --base-time now Test.log");
		parseResult.InvocationConfiguration.Output.WriteLine();
		parseResult.InvocationConfiguration.Output.WriteLine("  Forgot to set camera ahead one hour for Daylight Saving Time.");
		parseResult.InvocationConfiguration.Output.WriteLine("  Set Creation-Time of all JPG files in all folders ahead one hour:");
		parseResult.InvocationConfiguration.Output.WriteLine("    touchmax --recurse --set-files --set-creation --hour +1 *.jpg");
		parseResult.InvocationConfiguration.Output.WriteLine();
		parseResult.InvocationConfiguration.Output.WriteLine("  Set Modified-Time of text files to a month ago and ten minutes ahead:");
		parseResult.InvocationConfiguration.Output.WriteLine("    touchmax --set-files --set-modified --base-time now --month -1 --minute +10 *.txt");
		parseResult.InvocationConfiguration.Output.WriteLine();
		parseResult.InvocationConfiguration.Output.WriteLine("  Set Creation-Time to three days before the Modified-Time:");
		parseResult.InvocationConfiguration.Output.WriteLine("    touchmax --set-files --set-creation --base-time modified --day -3 *.txt");
		parseResult.InvocationConfiguration.Output.WriteLine();
		parseResult.InvocationConfiguration.Output.WriteLine("  Set a file's Modified-Time to 15 Sep 2008:");
		parseResult.InvocationConfiguration.Output.WriteLine("    touchmax --set-files --set-modified --year =2008 --month =9 --day =15 test.txt");
		parseResult.InvocationConfiguration.Output.WriteLine();

		return result;
	}
}

/// <summary>
/// Specifies the base timestamp to use before applying adjustments.
/// </summary>
internal enum BaseTimeMode
{
	None,
	Now,
	Created,
	Modified
}

/// <summary>
/// Pass a file path (with optional wildcards) and the offset to use to
/// change the file's last-modified time.
/// </summary>
/// <example>
/// TouchMax.exe +/-HH:MM <file specification>
/// </example>
public class Program
{
	private bool _isDryRun = false;

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

	private delegate bool DirectoryInfoHandler(DirectoryInfo dirinfo, string outputPrefix);
	private delegate bool FileInfoHandler(FileInfo dirinfo, string outputPrefix);


	/// <summary>
	/// Parses a date/time adjustment string that must start with +, -, or =.
	/// </summary>
	/// <param name="value">The value to parse (e.g., "+1", "-5", "=2023")</param>
	/// <returns>Tuple of (operation, numericValue) where operation is '+', '-', or '='</returns>
	/// <exception cref="ArgumentException">Thrown when value doesn't start with +, -, or =</exception>
	private static (char operation, int numericValue) ParseDateTimeAdjustment(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("Date/time adjustment value cannot be empty.");
		}

		char operation = value[0];
		if ((operation != '+') && (operation != '-') && (operation != '='))
		{
			throw new ArgumentException($"Date/time adjustment must start with +, -, or = (got: {value})");
		}

		if (value.Length < 2)
		{
			throw new ArgumentException($"Date/time adjustment must include a numeric value after {operation}");
		}

		string numericPart = value[1..];
		if (!int.TryParse(numericPart, out int numericValue))
		{
			throw new ArgumentException($"Invalid numeric value: {numericPart}");
		}

		// For '-' operation, negate the value
		if (operation == '-')
		{
			numericValue = -numericValue;
		}

		return (operation, numericValue);
	}

	/// <summary>
	/// Creates a validator for date/time adjustment options that validates the format.
	/// </summary>
	/// <param name="option">The option to validate</param>
	/// <returns>A validation action</returns>
	private static Action<System.CommandLine.Parsing.SymbolResult> CreateDateTimeAdjustmentValidator(Option<string?> option)
	{
		return result =>
		{
			string? value = result.GetValue(option);
			if (value != null)
			{
				try
				{
					ParseDateTimeAdjustment(value);
				}
				catch (ArgumentException ex)
				{
					result.AddError(ex.Message);
				}
			}
		};
	}


	static void Main(string[] args)
	{
		// Define all options with correct API
		Option<bool> setFilesOption = new("--set-files")
		{
			Description = "Set file timestamps"
		};

		Option<bool> setFoldersOption = new("--set-folders")
		{
			Description = "Set folder timestamps"
		};

		Option<bool> setCreationOption = new("--set-creation")
		{
			Description = "Change creation time"
		};

		Option<bool> setModifiedOption = new("--set-modified")
		{
			Description = "Change modified time"
		};

		Option<bool> recurseOption = new("--recurse")
		{
			Description = "Process subfolders"
		};

		Option<bool> dryRunOption = new("--dry-run")
		{
			Description = "Display changes without applying them"
		};

		Option<BaseTimeMode> baseTimeOption = new("--base-time")
		{
			Description = "Start with: now, created, or modified",
			DefaultValueFactory = _ => BaseTimeMode.None
		};

		Option<string?> yearOption = new("--year")
		{
			Description = "Adjust year (+N, -N, =N)"
		};
		yearOption.Validators.Add(CreateDateTimeAdjustmentValidator(yearOption));

		Option<string?> monthOption = new("--month")
		{
			Description = "Adjust month (+N, -N, =N) [January is 1 ]"
		};
		monthOption.Validators.Add(CreateDateTimeAdjustmentValidator(monthOption));

		Option<string?> dayOption = new("--day")
		{
			Description = "Adjust day (+N, -N, =N) [ Days are 1-31 ]"
		};
		dayOption.Validators.Add(CreateDateTimeAdjustmentValidator(dayOption));

		Option<string?> hourOption = new("--hour")
		{
			Description = "Adjust hour (+N, -N, =N)"
		};
		hourOption.Validators.Add(CreateDateTimeAdjustmentValidator(hourOption));

		Option<string?> minuteOption = new("--minute")
		{
			Description = "Adjust minute (+N, -N, =N)"
		};
		minuteOption.Validators.Add(CreateDateTimeAdjustmentValidator(minuteOption));

		// Define the pattern argument
		Argument<string> patternArgument = new("pattern")
		{
			Description = "File pattern (e.g., *.jpg, test.txt)"
		};

		// Create root command and add options/arguments
		RootCommand rootCommand = new("TouchMax changes the Creation-Time and/or Modified-Time of files or folders.");
		rootCommand.Options.Add(setFilesOption);
		rootCommand.Options.Add(setFoldersOption);
		rootCommand.Options.Add(setCreationOption);
		rootCommand.Options.Add(setModifiedOption);
		rootCommand.Options.Add(recurseOption);
		rootCommand.Options.Add(dryRunOption);
		rootCommand.Options.Add(baseTimeOption);
		rootCommand.Options.Add(yearOption);
		rootCommand.Options.Add(monthOption);
		rootCommand.Options.Add(dayOption);
		rootCommand.Options.Add(hourOption);
		rootCommand.Options.Add(minuteOption);
		rootCommand.Arguments.Add(patternArgument);

		// Add root-level validators
		rootCommand.Validators.Add(result =>
		{
			bool setFiles = result.GetValue(setFilesOption);
			bool setFolders = result.GetValue(setFoldersOption);
			if (!setFiles && !setFolders)
			{
				result.AddError("You must specify at least one of --set-files or --set-folders.");
			}
		});

		rootCommand.Validators.Add(result =>
		{
			bool setCreation = result.GetValue(setCreationOption);
			bool setModified = result.GetValue(setModifiedOption);
			if (!setCreation && !setModified)
			{
				result.AddError("You must specify at least one of --set-creation or --set-modified.");
			}
		});

		// Set the action
		rootCommand.SetAction(parseResult =>
		{
			bool setFiles = parseResult.GetValue(setFilesOption);
			bool setFolders = parseResult.GetValue(setFoldersOption);
			bool setCreation = parseResult.GetValue(setCreationOption);
			bool setModified = parseResult.GetValue(setModifiedOption);
			bool recurse = parseResult.GetValue(recurseOption);
			bool dryRun = parseResult.GetValue(dryRunOption);
			BaseTimeMode baseTime = parseResult.GetValue(baseTimeOption);
			string? year = parseResult.GetValue(yearOption);
			string? month = parseResult.GetValue(monthOption);
			string? day = parseResult.GetValue(dayOption);
			string? hour = parseResult.GetValue(hourOption);
			string? minute = parseResult.GetValue(minuteOption);
			string? pattern = parseResult.GetValue(patternArgument);

			// Create and configure program instance
			Program pgm = new();
			try
			{
				pgm._bSetFiles = setFiles;
				pgm._bSetFolders = setFolders;
				pgm._bSetCreation = setCreation;
				pgm._bSetModified = setModified;
				pgm._bRecurse = recurse;
				pgm._isDryRun = dryRun;

				// Set base time mode
				pgm._bUseNow = (baseTime == BaseTimeMode.Now);
				pgm._bUseCreation = (baseTime == BaseTimeMode.Created);
				pgm._bUseModified = (baseTime == BaseTimeMode.Modified);

				// Parse date/time adjustments
				if (year != null)
				{
					var (op, value) = ParseDateTimeAdjustment(year);
					if (op == '=')
					{
						pgm._absYear = value;
					}
					else
					{
						pgm._relYears = value;
					}
				}

				if (month != null)
				{
					var (op, value) = ParseDateTimeAdjustment(month);
					if (op == '=')
					{
						pgm._absMonth = value;
					}
					else
					{
						pgm._relMonths = value;
					}
				}

				if (day != null)
				{
					var (op, value) = ParseDateTimeAdjustment(day);
					if (op == '=')
					{
						pgm._absDate = value;
					}
					else
					{
						pgm._relDays = value;
					}
				}

				if (hour != null)
				{
					var (op, value) = ParseDateTimeAdjustment(hour);
					if (op == '=')
					{
						pgm._absHour = value;
					}
					else
					{
						pgm._relHours = value;
					}
				}

				if (minute != null)
				{
					var (op, value) = ParseDateTimeAdjustment(minute);
					if (op == '=')
					{
						pgm._absMinute = value;
					}
					else
					{
						pgm._relMinutes = value;
					}
				}

				// Parse pattern and directory
				if (!string.IsNullOrEmpty(pattern))
				{
					pgm._strPattern = Path.GetFileName(pattern);

					pgm._strDirectory = Path.GetDirectoryName(pattern) ?? ".";
					if (string.IsNullOrEmpty(pgm._strDirectory))
					{
						pgm._strDirectory = ".";
					}
				}

				// Display test mode info if requested
				if (pgm._isDryRun)
				{
					Console.WriteLine();
					Console.WriteLine("** DRY RUN MODE **");
					Console.WriteLine("Recurse? " + (pgm._bRecurse ? "YES" : "NO"));
					Console.WriteLine("Set files? " + (pgm._bSetFiles ? "YES" : "NO"));
					Console.WriteLine("Set folders? " + (pgm._bSetFolders ? "YES" : "NO"));
					Console.WriteLine("Now: " + pgm._dtNow.ToString());
					Console.WriteLine("Base: Use " + (pgm._bUseNow ? "NOW" : pgm._bUseCreation ? "CREATION" : "MODIFIED"));

					if ((pgm._absYear is not null) || (pgm._absMonth is not null) || (pgm._absDate is not null) || (pgm._absHour is not null) || (pgm._absMinute is not null))
					{
						Console.WriteLine("Absolute:");
						if (pgm._absYear.HasValue)
						{
							Console.WriteLine("  year = " + pgm._absYear);
						}

						if (pgm._absMonth.HasValue)
						{
							Console.WriteLine("  month = " + pgm._absMonth);
						}

						if (pgm._absDate.HasValue)
						{
							Console.WriteLine("  date = " + pgm._absDate);
						}

						if (pgm._absHour.HasValue)
						{
							Console.WriteLine("  hour = " + pgm._absHour);
						}

						if (pgm._absMinute.HasValue)
						{
							Console.WriteLine("  minute = " + pgm._absMinute);
						}
					}

					if ((pgm._relYears != 0) || (pgm._relMonths != 0) || (pgm._relDays != 0) || (pgm._relHours != 0) || (pgm._relMinutes != 0))
					{
						Console.WriteLine("Relative:");
						if (pgm._relYears != 0)
						{
							Console.WriteLine("  years = " + pgm._relYears);
						}

						if (pgm._relMonths != 0)
						{
							Console.WriteLine("  months = " + pgm._relMonths);
						}

						if (pgm._relDays != 0)
						{
							Console.WriteLine("  days = " + pgm._relDays);
						}

						if (pgm._relHours != 0)
						{
							Console.WriteLine("  hour = " + pgm._relHours);
						}

						if (pgm._relMinutes != 0)
						{
							Console.WriteLine("  minute = " + pgm._relMinutes);
						}
					}

					Console.WriteLine("Directory: " + pgm._strDirectory);
					Console.WriteLine("Pattern: " + pgm._strPattern);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error: " + ex.Message);
				return 1;
			}

			// Execute the touch operation
			pgm.Touch(pgm._strDirectory, pgm._strPattern);
			return 0;
		});

		// Parse and invoke
		ParseResult parseResult = rootCommand.Parse(args);
		Environment.Exit(parseResult.Invoke());
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

		string strIndent = string.Empty.PadLeft(nLevel * 2, ' ');
		Console.WriteLine(strIndent + dir.FullName + Path.AltDirectorySeparatorChar);

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
				if (dir.GetFiles(strPattern).Any(fileinfo => !handlerFileInfo(fileinfo, strIndent)))
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
				if (dir.GetDirectories(strPattern).Any(dirinfo => !handlerDirInfo(dirinfo, strIndent)))
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

	private bool OffsetFile(FileInfo fileinfo, string outputPrefix)
	{
		Console.WriteLine($"{outputPrefix}{fileinfo.Name}");

		DateTime dtCreation = File.GetCreationTime(fileinfo.FullName);
		DateTime dtModified = File.GetLastWriteTime(fileinfo.FullName);

		if (_bSetCreation)
		{
			DateTime dtNew = DetermineNewDateTime(dtCreation, dtCreation, dtModified);

			if (_isDryRun)
			{
				Console.WriteLine("{0}{1} => {2}", outputPrefix, dtCreation.ToString(), dtNew.ToString());
			}
			else
			{
				try
				{
					File.SetCreationTime(fileinfo.FullName, dtNew);
					Console.WriteLine("{0}{1} => {2}", outputPrefix, dtCreation.ToString(), File.GetCreationTime(fileinfo.FullName).ToString());
				}
				catch (IOException ex)
				{
					Console.WriteLine($"Unable to set Creation-Time of {fileinfo.Name} to {dtNew}.");
					Console.WriteLine(ex.Message);
				}
			}
		}

		if (_bSetModified)
		{
			DateTime dtNew = DetermineNewDateTime(dtModified, dtCreation, dtModified);

			if (_isDryRun)
			{
				Console.WriteLine("{0}{1} => {2}", outputPrefix, dtModified.ToString(), dtNew.ToString());
			}
			else
			{
				try
				{
					File.SetLastWriteTime(fileinfo.FullName, dtNew);
					Console.WriteLine("{0}{1} => {2}", outputPrefix, dtModified.ToString(), File.GetLastWriteTime(fileinfo.FullName).ToString());
				}
				catch (IOException ex)
				{
					Console.WriteLine($"Unable to set Modified-Time of {fileinfo.Name} to {dtNew}.");
					Console.WriteLine(ex.Message);
				}
			}
		}

		return true;
	}

	private bool OffsetDirectory(DirectoryInfo dirinfo, string outputPrefix)
	{
		Console.WriteLine($"{outputPrefix}{dirinfo.Name}/");

		DateTime dtCreation = Directory.GetCreationTime(dirinfo.FullName);
		DateTime dtModified = Directory.GetLastWriteTime(dirinfo.FullName);
		if (_bSetCreation)
		{
			DateTime dtNew = DetermineNewDateTime(dtCreation, dtCreation, dtModified);

			if (_isDryRun)
			{
				Console.WriteLine("{0}{1} => {2}", outputPrefix, dtCreation.ToString(), dtNew.ToString());
			}
			else
			{
				try
				{
					Directory.SetCreationTime(dirinfo.FullName, dtNew);
					Console.WriteLine("{0}{1} => {2}", outputPrefix, dtCreation.ToString(), Directory.GetCreationTime(dirinfo.FullName).ToString());
				}
				catch (IOException ex)
				{
					Console.WriteLine($"Unable to set Creation-Time of {dirinfo.Name} to {dtNew}.");
					Console.WriteLine(ex.Message);
				}
			}
		}

		if (_bSetModified)
		{
			DateTime dtNew = DetermineNewDateTime(dtModified, dtCreation, dtModified);

			if (_isDryRun)
			{
				Console.WriteLine("{0}{1} => {2}", outputPrefix, dtModified.ToString(), dtNew.ToString());
			}
			else
			{
				try
				{
					Directory.SetLastWriteTime(dirinfo.FullName, dtNew);
					Console.WriteLine("{0}{1} => {2}", outputPrefix, dtModified.ToString(), Directory.GetLastWriteTime(dirinfo.FullName).ToString());
				}
				catch (IOException ex)
				{
					Console.WriteLine($"Unable to set Modified-Time of {dirinfo.Name} to {dtNew}.");
					Console.WriteLine(ex.Message);
				}
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
}
