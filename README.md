<h1 align="center"><img align="center" src=".media/icon-msdf-atlas-gen.png"/> SharpMSDF <img align="center" src=".media/icon-msdfgen.png"/></h1>

C#/.NET Port of Chlumsky's [msdfgen](https://github.com/Chlumsky/msdfgen) and [msdf-atlas-gen](https://github.com/Chlumsky/msdfgen) 
written in pure C# with no native dlls included !


More specifically version 1.12.1 of msdfgen

## Features
- ✅ No native dependencies (should work on any platform that works with .NET 8.0+).
- ✅ OpenType (ttf/otf/..) font loader.
- ✅ SDF/PSDF/MSDF/MTSDF generator.
- ✅ bmp and png image formats Exporter.
- ✅ MSDF Atlas generator.
- ✅ Dynamic Atlas (Load glyphs on-the-fly when needed).
- ✅ Optionally uses Skia for full shape processing. (uses native libs)

> Note : some features are not implemented, such as Mulit-threading.

## Usage
You can check demo for use !

## Feedback
This port doesn't cover the whole project, If you find any bugs or want to add some missing stuff, you can post an Issue or PR.

## Licenses

MIT License 2025 FenzDev

MIT License 2014-2025, Viktor Chlumsky

(LayoutFarm/Typography licenses can be seen in header of its source files) 

## References

OpenType: 
- https://github.com/LayoutFarm/Typography/tree/master/Typography.OpenFont

MSDF:
- https://github.com/Chlumsky/msdfgen
- https://github.com/Chlumsky/msdf-atlas-gen
- https://github.com/DWVoid/Msdfgen.Net