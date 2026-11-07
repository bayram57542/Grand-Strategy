@echo off
chcp 65001 >nul
set MESSAGE=%~1
if "%MESSAGE%"=="" set MESSAGE=Avtomatik Ehtiyat Nusxe (Backup): %date% %time%

echo [1/3] Deyisiklikler elave edilir (git add .)...
git add .

echo [2/3] Lokal bazaya yazilir (git commit)...
git commit -m "%MESSAGE%"

echo [3/3] GitHub-a gonderilir (git push)...
git remote | findstr "origin" >nul
if %errorlevel%==0 (
    git push -u origin main
) else (
    echo [Melumat] Lokal commit tamamlandi. GitHub 'origin' hele elave edilmeyib.
)
