# GiawaOS
This is a place for me to toy around with what might be necessary to build an operating system using C#.  The roadmap looks something like this:

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