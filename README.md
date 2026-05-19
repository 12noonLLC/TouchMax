# TouchMax by [12noon LLC](https://12noon.com)

[![Build](https://github.com/12noonLLC/TouchMax/actions/workflows/build.yml/badge.svg)](https://github.com/12noonLLC/TouchMax/actions/workflows/build.yml)

Did you forget to set your camera's time to Daylight Saving Time? TouchMax can
fix it as well of many other issues you might encounter with the creation
or modification timestamps of your files and folders.

TouchMax changes the Creation-Time and/or Modified-Time of any files or folders.
It can use an absolute time or one relative to the file's creation time,
file's modified time, or current time.

## Usage

`TouchMax.exe [options] <pattern>`

| Option | Description |
| :----- | :---------- |
| --dry-run | Display values without making changes
| --set-files | set file timestamps
| --set-folders | set folder timestamps
| --recurse | process subfolders
| --set-creation | change creation time of the files
| --set-modified | change modified time of the files

Optionally, add the following options to increment, decrement, or set a date/time component.

| Option | Description |
| :----- | :---------- |
| --year +# or --year -# or --year =#  | set the year
| --month +# or --month -# or --month =# | set the month (January is 1, etc.)
| --day +# or --day -# or --day =# | set the date (1-31)
| --hour +# or --hour -# or --hour =#  | set the hour (0-23)
| --minute +# or --minute -# or --minute =#  | set the minute (0-59)

	+ increments that value by the specified number
	- decrements that value by the specified number
	= sets that value to the specified number

Optionally, add the following option to establish a base timestamp:

| Option | Description |
| :----- | :---------- |
| --base-time now | set timestamp to current time before modifying
| --base-time created | set timestamp to file's creation time before modifying
| --base-time modified | set timestamp to file's modified time before modifying

## Process

TouchMax follows these steps to set a file or folder's timestamp:

	 1. Set timestamp to now (or to the creation time or to the modified time),
		if `--base-time` is specified.
	 2. Apply absolute values, if any.
	 3. Apply relative changes, if any.

## Examples

Set *Modified-Time* of a file to the current date/time:

	touchmax.exe --set-files --set-modified --base-time now Test.log

If you forgot to set your camera's time ahead one hour for Daylight Saving Time,
you can set the *Creation-Time* of all JPG files in folder ahead one hour:

	touchmax.exe --set-files --set-creation --hour +1 *.jpg

Set *Modified-Time* of text files to last month and ten minutes ahead:

	touchmax.exe --set-files --set-modified --base-time now --month -1 --minute +10 *.txt

Set *Creation-Time* to three days before the *Modified-Time*:

	touchmax.exe --set-files --set-creation --base-time modified --day -3 *.txt

Set a file's *Modified-Time* to 15 Sep 2008:

	touchmax.exe --set-files --set-modified --year =2008 --month =9 --day =15 test.txt

