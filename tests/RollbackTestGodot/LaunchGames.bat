@echo off
@pushd %~dp0

:: Ask how many games to launch
SET /P "GAMES_COUNT=How many games to launch [default: 2]?" || SET "GAMES_COUNT=2"

:: Start games
FOR /L %%G IN (1, 1, %GAMES_COUNT%) DO (
	SET NUMBER_OF_PLAYERS=%GAMES_COUNT%
	START "" "%GODOT_EXECUTABLE%" --path "."
)

@popd
