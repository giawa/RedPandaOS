# RedPandaOS
This is a place to toy around with what might be necessary to build an operating system using C#.  The roadmap looks something like this:

- Understand IL by:
  - ✔️ Processing PE files
  - ✔️ Extracting IL and metadata
  - ✔️ Intepreting basic IL
- Understand ~~x86_64~~ (x86 for now) by:
  - ✔️ Building a few simple programs and assembling them with NASM or similar
  - ✔️ Emit asm from C# and automate the assembler process
  - ~~Modify IL interpreter to emit asm and run that instead~~
  - ✔️ (Skipped) ~~Eventually this IL interpreter should be able to self-host (even if it is way slower when done this way)~~
- Boot
  - ✔️ (Decided to go with C# only) ~~Write a simple OS in asm/C first and set up linker/etc scripts so that it can run~~
  - ✔️ Create C# attributes or other tech to enable C# and the modified interpreter to emit asm that can be assembled into a bootable image
  - ✔️ (We're C# and asm all the way) ~~Utilize C where necessary, but target rewriting in C#~~
- Basic
  - ✔️ Interrupts
  - ✔️ Paging
  - ✔️ Multitasking
- Drivers
  - ✔️ Keyboard
  - Mouse
  - ✔️ Text mode
  - VGA
  - Network
  - ✔️ Storage (✔️ PATA, ATAPI, SATA, NVMe)
  - File system (exFAT, FAT32?)
  - USB

## Getting Started
1) Install your preferred C# development environment (I use Visual Studio 2022)
2) Install qemu (https://www.qemu.org/download/) and make sure it is in the PATH (you should be able to run `qemu-system-x86_64` from the command line)
3) Install nasm (https://www.nasm.us/pub/nasm/releasebuilds/?C=M;O=D) and make sure it is in the PATH (you should be able to run `nasm` from the command line)
4) Clone this github repo to your computer
5) Open the RedPandaOS solution file
6) Make sure submodules have been initialized, as you will need the elfsharp project (which is included as a submodule)
7) Make sure RedPandaOS is selected as the startup project
8) Build and and run the solution.  A command window should appear and then qemu should launch, booting the OS.

## Features
### Project
- Built from scratch portable executable (PE) file loader, sample interpreter, IL to assembly converter, and operating system.  Only dependencies are the virtual machine you use (qemu), NASM for assembling the generated assembly and the ElfSharp codebase for symbol file generation.
- Two stage bootloader
- 32bit Operating System written in C#
- Write plugs for System namespace methods using C# attributes (in either C# or assembly)

### Red Panda OS (Operating System)
- Interrupts
- Paging
- Kernel malloc
- Exceptions and stack traces (using symbol information)
- PCI Bus Enumeration
- PATA Driver
- Keyboard Driver
- VGA Text Driver

### IL2Asm (x86 Code Generation)
- Relatively stable x86 assembly code generation from IL
- Support for generics with one generic type (List<T> works, Dictionary<T, K> does not)
- Support for the new keyword (assuming a malloc function is supplied by the OS, which itself can be written in C#)
- Support for struct and class, all value types except 64 bit
- Floating point support
- Support for throw (no support for try/catch yet)

## Code Layout
The RedPandaOS solution contains multiple projects.
- CPUHelper contains methods that the OS can call, and their assembly plugs
- IL2Asm.BaseTypes contains types that IL2Asm uses and must share with other projects
- IL2Asm is what converts .NET IL code to assembly (x86 or otherwise)
- ILInterpreter is a deprecated project that explores how to process and execute IL code from within C#
- ILInterpreter.Tests is a deprecated project that has some unit testing for ILInterpreter
- PELoader processes portable executable (PE) files and extracts the .NET metadata
- RedPandaOS is the bootloader, operating system and program that calls nasm and qemu
- TestIL is a test application that generates some IL that can be used by ILInterprester or IL2Asm

## Contributing
Contributions via pull requests are welcome!  Please ensure you follow the existing coding conventions used by the project.  I use the default C# formatting options in Visual Studio.  Private variables should be prefixed with `_`.  In general I try to follow the .NET Core coding conventions (https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md).

## Screenshots:

![image](https://user-images.githubusercontent.com/3923687/116469294-d32b8380-a826-11eb-8555-335d2a64fdae.png)
![image](https://user-images.githubusercontent.com/3923687/147905793-06953fb5-3cbb-4ebe-b4e3-eb7e1de977cf.png)
