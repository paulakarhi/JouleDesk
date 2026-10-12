# Third-party notices

JouleDesk itself is licensed under the MIT License (see `LICENSE`). The single-file `JouleDesk.exe` that its installer
installs also contains the software below, and the installer is built with Inno Setup. Each component keeps its own
license; the full texts are in the `licenses` folder or at the end of this file. The source code of every component is
public at the address given.

## .NET

- The .NET runtime (Microsoft.NETCore.App 9.0.8), its class libraries and the Windows Desktop runtime
  (Microsoft.WindowsDesktop.App 9.0.8, which holds Windows Forms) are included in the self-contained `JouleDesk.exe`.
- License: MIT (below)
- The runtime in turn contains components under other licenses. Their notices, as published with .NET 9.0.8, are in
  `licenses/dotnet-THIRD-PARTY-NOTICES.txt`.
- Source: https://github.com/dotnet/runtime and https://github.com/dotnet/winforms

## System.Management

- Version 9.0.0, compiled into `JouleDesk.exe` unmodified. It reads the Windows energy meter, the drives and the memory
  modules through WMI.
- License: MIT (below)
- Source: https://github.com/dotnet/runtime

## Inno Setup

- The installer is built with Inno Setup 7, and its setup program is part of the installer. It is not part of
  `JouleDesk.exe`.
- License: the Inno Setup License (`licenses/InnoSetup-license.txt`)
- Website: https://jrsoftware.org/

## MIT License

Applies to the .NET components and System.Management.
Copyright (c) .NET Foundation and Contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of
the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Data, not software

The spot prices come from the free [Pörssisähkö.net](https://porssisahko.net/) service, which publishes the Nord Pool
day-ahead prices for Finland. JouleDesk fetches them at run time and does not include them. The drive profiles in
`catalog-example.json` use the figures in Kingston's published specifications.
