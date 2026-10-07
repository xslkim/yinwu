@echo off
rem tvgun-bridge one-time admin setup: urlacl + firewall rules
rem Right-click -> Run as administrator
netsh http add urlacl url=http://+:8000/ user=%USERDOMAIN%\%USERNAME%
netsh advfirewall firewall add rule name="tvgun-bridge TCP 8000" dir=in action=allow protocol=TCP localport=8000
netsh advfirewall firewall add rule name="tvgun-bridge UDP 8000" dir=in action=allow protocol=UDP localport=8000
netsh advfirewall firewall add rule name="tvgun-bridge UDP 8001" dir=in action=allow protocol=UDP localport=8001
echo.
echo Done. Daily use does not need admin.
pause
