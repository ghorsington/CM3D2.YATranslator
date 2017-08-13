@echo off
rem Basic build script for Maid Fiddler
rem Requires MSBuild 15 with C# 7.0 compatible compiler
rem NOTE: PLACE THE NEEDED ASSEMBLIES INTO "Libs" FOLDER
rem You may specify the build configuration as an argument to this batch
rem If no arguments are specified, will build the Release version

rem SOLUTION-SPECIFIC VARIABLES:

set hook=CM3D2.YATranslator.Hook
set plugin=CM3D2.YATranslator.Plugin
set patchersybaris=CM3D2.YATranslator.Sybaris.Patcher
set patcherrei=CM3D2.YATranslator.Patch
set subdump=CM3D2.SubtitleDumper

rem SOLUTION-SPECIFIC VARIABLES END

echo Locating MSBuild

set libspath=%cd%\Libs
set buildconf=Release
set buildplat=AnyCPU
set vswhere="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

for /f "usebackq tokens=*" %%i in (`%vswhere% -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
	set InstallDir=%%i
)

set msbuild="%InstallDir%\MSBuild\15.0\Bin\MSBuild.exe"

if not exist %msbuild% (
	echo Failed to locate MSBuild.exe
	echo This project uses MSBuild 15 to compile
	pause
	exit /b 1
)

if not -%1-==-- (
	echo Using %1 as building configuration
	set buildconf=%1
)
if -%1-==-- (
	echo No custom build configuration specified. Using Release
)

if not -%2-==-- (
	echo Using %2 as building platform
	set buildplat=%2
)
if -%2-==-- (
	echo No custom platform specified. Using AnyCPU
)

if not [%hook%] == [] (
	rmdir /Q /S "%cd%\%hook%\bin\%buildconf%" >NUL
	rmdir /Q /S "%cd%\%hook%\obj" >NUL

	%msbuild% "%cd%\%hook%\%hook%.csproj" /p:Configuration=%buildconf%,Platform=%buildplat%

	if not %ERRORLEVEL%==0 (
		echo Failed to compile Hook! Make sure you have all the needed assemblies in the "Libs" folder!
	)
)

if not [%patcherrei%] == [] (
	rmdir /Q /S "%cd%\%patcherrei%\bin\%buildconf%" >NUL
	rmdir /Q /S "%cd%\%patcherrei%\obj" >NUL
	
	%msbuild% "%cd%\%patcherrei%\%patcherrei%.csproj" /p:Configuration=%buildconf%,Platform=%buildplat%

	if not %ERRORLEVEL%==0 (
		echo Failed to compile Patch! Make sure you have all the needed assemblies in the "Libs" folder!
	)
)

if not [%patchersybaris%] == [] (
	rmdir /Q /S "%cd%\%patchersybaris%\bin\%buildconf%" >NUL
	rmdir /Q /S "%cd%\%patchersybaris%\obj" >NUL
	
	%msbuild% "%cd%\%patchersybaris%\%patchersybaris%.csproj" /p:Configuration=%buildconf%,Platform=%buildplat%

	if not %ERRORLEVEL%==0 (
		echo Failed to compile Sybaris Patch! Make sure you have all the needed assemblies in the "Libs" folder!
	)
)

if not [%plugin%] == [] (
	rmdir /Q /S "%cd%\%plugin%\bin\%buildconf%" >NUL
	rmdir /Q /S "%cd%\%plugin%\obj" >NUL

	%msbuild% "%cd%\%plugin%\%plugin%.csproj" /p:Configuration=%buildconf%,Platform=%buildplat%

	if not %ERRORLEVEL%==0 (
		echo Failed to compile Plugin! Make sure you have patched Assembly-CSharp and copied it into "Libs" folder!
	)
)

if not [%subdump%] == [] (
	rmdir /Q /S "%cd%\%subdump%\bin\%buildconf%" >NUL
	rmdir /Q /S "%cd%\%subdump%\obj" >NUL

	%msbuild% "%cd%\%subdump%\%subdump%.csproj" /p:Configuration=%buildconf%,Platform=%buildplat%

	if not %ERRORLEVEL%==0 (
		echo Failed to compile Subtitle dumper!
	)
)

mkdir Build
move /Y "%cd%\%hook%\bin\%buildconf%\%hook%.dll" "%cd%\Build\%hook%.dll"
move /Y "%cd%\%patcherrei%\bin\%buildconf%\%patcherrei%.dll" "%cd%\Build\%patcherrei%.dll"
move /Y "%cd%\%patchersybaris%\bin\%buildconf%\%patchersybaris%.dll" "%cd%\Build\%patchersybaris%.dll"
move /Y "%cd%\%plugin%\bin\%buildconf%\%plugin%.dll" "%cd%\Build\%plugin%.dll"
move /Y "%cd%\%subdump%\bin\%buildconf%\*" "%cd%\Build"

echo All done!
pause