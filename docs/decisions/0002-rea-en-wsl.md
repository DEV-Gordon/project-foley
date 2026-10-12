# 0002 - REA y Ghidra corren en WSL

- El soporte de Ghidra de REA en Windows solo admite ejecutables x86-64 y no admite DLL.
- CoD1 (2003) es x86 de 32 bits, asi que REA corre en WSL (Ubuntu) con Ghidra 12.1.4 y JDK 21.
- Repo, Godot, .NET y Claude Code corren en Windows. Claude Code llama a REA a traves de wsl.exe.
- Las rutas que se le pasan a REA son rutas Linux: ~/foley/binarios/...
