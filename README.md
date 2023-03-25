# TouchMax by [12noon.com](https://12noon.com)

[![.NET](https://github.com/skst/TouchMax/actions/workflows/dotnet.yml/badge.svg)](https://github.com/skst/TouchMax/actions/workflows/dotnet.yml)

Did you forget to set your camera's time to Daylight Saving Time? TouchMax can
fix it as well of many other issues you might encounter with the creation
or modification timestamps of your files and folders.

TouchMax changes the Creation-Time and/or Modified-Time of any files or folders.
It can use an absolute time or one relative to the file's creation time,
file's modified time, or current time.

## Usage

`TouchMax.exe [switches] <pattern>`

| Switch | Description |
| :----- | :---------- |
| /test | Display values without making changes
| /setfiles | set file timestamps
| /setfolders | set folder timestamps
| /recurse | process subfolders
| /setcreation | change creation time of the files
| /setmodified | change modified time of the files

Optionally, add the following switches to increment, decrement, or set a date/time component.

| Switch | Description |
| :----- | :---------- |
| /Y+# or /Y-# or /Y=# | set the year
| /M+# or /M-# or /M=# | set the month (January is 1, etc.)
| /D+# or /D-# or /D=# | set the date (1-31)
| /h+# or /h-# or /h=#  | set the hour (1-12)
| /m+# or /m-# or /m=#  | set the minute (0-59)

	+ increments that value by the specified number
	- decrements that value by the specified number
	= sets that value to the specified number

Optionally, add one of the following switches to establish a base timestamp:

| Switch | Description |
| :----- | :---------- |
| /usenow | set timestamp to current time before modifying
| /usecreation | set timestamp to file's creation time before modifying
| /usemodified | set timestamp to file's modified time before modifying

## Process

TouchMax follows these steps to set a file or folder's timestamp:

	 1. Set timestamp to now (or to the creation time or to the modified time),
		if `/usenow` (or `/usecreation` or `/usemodified`) is specified.
	 2. Apply absolute values, if any.
	 3. Apply relative changes, if any.

## Examples

Set *Modified-Time* of a file to the current date/time:

	touchmax.exe /setfiles /setmodified /usenow Test.log

If you forgot to set your camera's time ahead one hour for Daylight Saving Time,
you can set the *Creation-Time* of all JPG files in folder ahead one hour:

	touchmax.exe /setfiles /setcreation /h+1 *.jpg

Set *Modified-Time* of text files to last month and ten minutes ahead:

	touchmax.exe /setfiles /setmodified /usenow /M-1 /m+10 *.txt

Set *Creation-Time* to three days before the *Modified-Time*:

	touchmax.exe /setfiles /setmodified /usemodified /D-3 *.txt

Set a file's *Modified-Time* to 15 Sep 2008:

	touchmax.exe /setfiles /setmodified /usemodified /Y=2008 /M=9 /D=15 test.txt

