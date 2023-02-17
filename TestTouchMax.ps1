#
# Set-ExecutionPolicy Unrestricted -Scope CurrentUser
#

set-psdebug –strict


#
#   Perform a single test.
#   If a passed "expected" date/time object is $Null, there should be no change.
#
function TestRun(
					  [string]$strName,
					  [System.DateTime]$dtInitialCreation,
					  [System.DateTime]$dtInitialModified,
					  [string]$argsOperation,
					  # Since these can be passed $Null, we can't specify the DateTime data type--it isn't Nullable.
					  $dtExpectedCreation,
					  $dtExpectedModified)
{
	Write-Host
	Write-Host
	Write-Host -foregroundColor blue $strName

	$fn = "test.txt"

	#New-Item -path $fn -type file | Out-Null
	Set-Content $fn "Test content"

	#
	#	Set the initial times
	#
	$f = Get-ChildItem $fn
	$f.CreationTime = $dtInitialCreation
	$f.LastWriteTime = $dtInitialModified

#	Write-Host 'Creation:' $f.CreationTime
#	Write-Host 'Modified:' $f.LastWriteTime

	#
	#	Perform the TouchMax operation
	#
#	.\publish\TouchMax.exe
#	.\bin\Release\net6.0\TouchMax.exe
#	.\bin\Debug\net6.0\TouchMax.exe
	if (-not(Test-Path '.\publish\TouchMax.exe'))
	{
		throw 'Executable does not exist.'
	}
	.\publish\TouchMax.exe $argsOperation.Split() $fn

	#
	#	Test the expected timestamps
	#
	$f = Get-ChildItem $fn
	if ($dtExpectedCreation -eq $Null)
	{
		$dtExpectedCreation = [System.DateTime]$dtInitialCreation
	}
	$span = $f.CreationTime - $dtExpectedCreation
	if (($span.TotalSeconds -gt -1) -and ($span.TotalSeconds -lt 1))
	{
		Write-Host -foregroundColor green "Success"
	}
	else
	{
		Write-Error("CreationTime is " + $f.CreationTime.ToString() + " (expected " + $dtExpectedCreation.ToString() + ")")
	}

	if ($dtExpectedModified -eq $Null)
	{
		$dtExpectedModified = [System.DateTime]$dtInitialModified;
	}
	$span = $f.LastWriteTime - $dtExpectedModified
	if (($span.TotalSeconds -gt -1) -and ($span.TotalSeconds -lt 1))
	{
		Write-Host -foregroundColor green "Success"
	}
	else
	{
		Write-Error("ModifiedTime is " + $f.LastWriteTime.ToString() + " (expected " + $dtExpectedModified.ToString() + ")")
	}

	Remove-Item $fn
}


#
# Set Creation exactly
#
$dtCreation = New-Object -typename System.DateTime(2001, 12, 1, 3, 15, 0)
$dtModified = New-Object -typename System.DateTime(2003, 6, 8, 11, 58, 0)
$argsOperation = "/setfiles /setcreation /Y=2005 /M=8 /D=12 /h=8 /m=42"
$dtExpectedCreation = New-Object -typename System.DateTime(2005, 8, 12, 8, 42, 0)
TestRun "Set Creation exactly" $dtCreation $dtModified $argsOperation $dtExpectedCreation $Null

#
# Set Modified exactly
#
$dtCreation = New-Object -typename System.DateTime(2001, 12, 1, 3, 15, 0)
$dtModified = New-Object -typename System.DateTime(2003, 6, 8, 11, 58, 0)
$argsOperation = "/setfiles /setmodified /Y=2006 /M=4 /D=30 /h=15 /m=9"
$dtExpectedModified = New-Object -typename System.DateTime(2006, 4, 30, 15, 9, 0)
TestRun "Set Modified exactly" $dtCreation $dtModified $argsOperation $Null $dtExpectedModified


#
# Set Creation to Modified -2 months
#
$dtCreation = New-Object -typename System.DateTime(2001, 12, 1, 3, 15, 0)
$dtModified = New-Object -typename System.DateTime(2003, 6, 8, 11, 58, 0)
$argsOperation = "/setfiles /setcreation /usemodified /M-2"
$dtExpectedCreation = $dtModified.AddMonths(-2)
TestRun "Set Creation to Modified -2 months" $dtCreation $dtModified $argsOperation $dtExpectedCreation $Null

#
# Set Modified to Creation +1 day
#
$dtCreation = New-Object -typename System.DateTime(2001, 12, 1, 3, 15, 0)
$dtModified = New-Object -typename System.DateTime(2003, 6, 8, 11, 58, 0)
$argsOperation = "/setfiles /setmodified /usecreation /D+1"
$dtExpectedModified = $dtCreation.AddDays(1)
TestRun "Set Modified to Creation +1 day" $dtCreation $dtModified $argsOperation $Null $dtExpectedModified


#
# Set Creation to Now -6 years, +4 months, -7 hours
#
$dtNow = Get-Date
$dtCreation = New-Object -typename System.DateTime(2001, 12, 1, 3, 15, 0)
$dtModified = New-Object -typename System.DateTime(2003, 6, 8, 11, 58, 0)
$argsOperation = "/setfiles /setcreation /usenow /Y-6 /M+4 /h-7"
$dtExpectedCreation = $dtNow
$dtExpectedCreation = $dtExpectedCreation.AddYears(-6)
$dtExpectedCreation = $dtExpectedCreation.AddMonths(4)
$dtExpectedCreation = $dtExpectedCreation.AddHours(-7)
TestRun "Set Creation to Now -6 years, +4 months, -7 hours" $dtCreation $dtModified $argsOperation $dtExpectedCreation $Null

#
# Set Modified to Now -4 years, +3 days, -26 minutes
#
$dtCreation = New-Object -typename System.DateTime(2001, 12, 1, 3, 15, 0)
$dtModified = New-Object -typename System.DateTime(2003, 6, 8, 11, 58, 0)
$argsOperation = "/setfiles /setmodified /usenow /Y-4 /D+3 /m-26"
$dtExpectedModified = $dtNow
$dtExpectedModified = $dtExpectedModified.AddYears(-4)
$dtExpectedModified = $dtExpectedModified.AddDays(3)
$dtExpectedModified = $dtExpectedModified.AddMinutes(-26)
TestRun "Set Modified to Now -4 years, +3 days, -26 minutes" $dtCreation $dtModified $argsOperation $Null $dtExpectedModified




#TestRun $dtCreation $dtModified "/setfiles /setmodified /Y= /M= /D= /h= /m=" $dtExpectedCreation $dtExpectedModified
#	.\bin\Debug\net5.0\TouchMax.exe /setfiles /setcreation ("/Y={0}" -f $dtExpectedCreation.Year) ("/M={0}" -f $dtExpectedCreation.Month) ("/D={0}" -f $dtExpectedCreation.Day) ("/h={0}" -f $dtExpectedCreation.Hour) ("/m={0}" -f $dtExpectedCreation.Minute) $fn
#"/setfiles /setmodified /Y=2006 /M=4 /D=30 /h=15 /m=9"
