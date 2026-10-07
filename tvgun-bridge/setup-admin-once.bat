@echo off
rem tvgun-bridge 一次性管理员设置：HTTP 端口预留 + 防火墙放行
rem 右键 -> 以管理员身份运行
netsh http add urlacl url=http://+:8000/ user=%USERDOMAIN%\%USERNAME%
netsh advfirewall firewall add rule name="tvgun-bridge TCP 8000" dir=in action=allow protocol=TCP localport=8000
netsh advfirewall firewall add rule name="tvgun-bridge UDP 8000" dir=in action=allow protocol=UDP localport=8000
netsh advfirewall firewall add rule name="tvgun-bridge UDP 8001" dir=in action=allow protocol=UDP localport=8001
echo.
echo 完成。以后日常使用不需要管理员权限。
pause
