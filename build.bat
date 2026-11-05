@echo off
chcp 65001 >nul
title RA2-LiveController Build Script
echo ========================================================
echo  RA2-LiveController 单文件自包含独立发布构建 (win-x86)
echo ========================================================
echo.

where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [错误] 未检测到 .NET SDK，请先安装 .NET 10.0 SDK:
    echo https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [*] 正在还原依赖并编译发布单文件 exe...
dotnet publish "%~dp0RA2LiveController.csproj" -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo  [成功] 编译完成！单文件独立免安装版可执行程序位于:
    echo  bin\Release\net10.0-windows\win-x86\publish\RA2LiveController.exe
    echo ========================================================
) else (
    echo.
    echo [失败] 构建过程发生错误，请检查控制台输出信息。
)

echo.
pause
