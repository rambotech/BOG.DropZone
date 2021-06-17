REM Start for Windows 10

PUSHD %~0d%~0p

BOG.DropZone.exe --AccessToken YourAccessTokenValueHere --AdminToken YourAdminTokenValueHere --MaxDropzones 5 --MaximumFailedAttemptsBeforeLockout 3 --LockoutSeconds 300

REM With https support
REM BOG.DropZone.exe --AccessToken YourAccessTokenValueHere --AdminToken YourAdminTokenValueHere --MaxDropzones 5 --MaximumFailedAttemptsBeforeLockout 3 --LockoutSeconds 300 --HttpsPort 5001

POPD