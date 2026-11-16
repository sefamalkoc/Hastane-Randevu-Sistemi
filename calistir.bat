@echo off
chcp 65001 > nul
title Hastane ve Klinik Randevu Sistemi - Web Sunucusu
echo =================================================================
echo   HASTANE / KLINIK RANDEVU SISTEMI - WEB SUNUCUSU BASLATILIYOR
echo =================================================================
echo.
echo   Tarayıcınız otomatik olarak http://localhost:5000 adresinde açılacaktır.
echo   Kapatmak için bu pencerede Ctrl + C tuşlarına basabilirsiniz.
echo.

cd /d "%~dp0"
start "" http://localhost:5000
dotnet run --project RandevuSistemi
pause
