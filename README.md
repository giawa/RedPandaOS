# RedPandaOS
This is a place to toy around with what might be necessary to build an operating system using C#.  The roadmap looks something like this:

- Understand IL by:
  - Processing PE files
  - Extracting IL and metadata
  - Intepreting basic IL
- Understand x86_64 by:
  - Building a few simple programs and assembling them with NASM or similar
  - Emit asm from C# and automate the assembler process
  - Modify IL interpreter to emit asm and run that instead
  - Eventually this IL interpreter should be able to self-host (even if it is way slower when done this way)
- Boot
  - Write a simple OS in asm/C first and set up linker/etc scripts so that it can run
  - Create C# attributes or other tech to enable C# and the modified interpreter to emit asm that can be assembled into a bootable image
  - Utilize C where necessary, but target rewriting in C#
- Drivers
  - Keyboard, mouse
  - Text mode
  - VGA
  - Network
  - Storage (exFAT)

## Getting Started
1) Install your preferred C# development environment (I use Visual Studio 2022)
2) Install qemu (https://www.qemu.org/download/) and make sure it is in the PATH (you should be able to run `qemu-system-x86_64` from the command line)
3) Install nasm (https://www.nasm.us/pub/nasm/releasebuilds/?C=M;O=D) and make sure it is in the PATH (you should be able to run `nasm` from the command line)
4) Clone this github repo to your computer
5) Open the RedPandaOS solution file
6) Make sure RedPandaOS is selected as the startup project
7) Build and and run the solution.  A command window should appear and then qemu should launch, booting the OS.

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
  
## Some design thoughts:
- Everything malloc'd is garbage collected, a free just drops the reference and doesn't immediately free it
- PE is the executable file format of choice, allowing standard .NET files to be run (I hope)
- Eventually everything should be written in C#, but perhaps a subset that allows easier targeting of asm
- With sufficient privileges, code can be patched/modified at runtime (even kernel code)

## Screenshots:

![image](https://user-images.githubusercontent.com/3923687/116469294-d32b8380-a826-11eb-8555-335d2a64fdae.png)
