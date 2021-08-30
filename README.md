# Source2Roblox

A super awesome C# console application that can:
- Rip data from Valve's game files (*.VPK, *.VMT, *.VTF, *.BSP)
- Compile that data into files that Roblox can work with (*.RBXM, *.RBXL, *.MESH, *.PNG)

# Usage

This program is still in development and isn't user-friendly.<br/>
But if you're feeling brave enough, you can set it up with the following steps:

1. Install Visual Studio 2019 with `Visual C#` and `.NET Framework 4.7.2`
2. Fork the following GitHub repositories into a single directory:
   - https://github.com/CloneTrooper1019/Source2Roblox
   - https://github.com/CloneTrooper1019/ValveKeyValue
   - https://github.com/CloneTrooper1019/Roblox-File-Format
3. Open the solution file `Source2Roblox.sln`
4. Right click on the `Source2Roblox` project and click Properties
5. Navigate to the `Debug` tab, and use some of the following command line arguments to get things up and running:

| **Argument**                         | **Required?** | **Example**                                                                         |
|--------------------------------------|---------------|-------------------------------------------------------------------------------------|
| `-game "PATH/TO/GAME/FOLDER"`        | **YES**       | `-game "C:\Program Files (x86)\Steam\steamapps\common\Half-Life 2\hl2"`             |
| `-model "path/to/local/model.mdl"`   | No            | `-model` for real-time model searching,<br/>`-model gman_high` for a specific model |
| `-vtf "path/to/local/image.vtf"`     | No            | `-vtf "editor/obsolete.vtf"`                                                        |
| `-map "map_name"`                    | No            | `-map d1_trainstation_01`                                                           |

With the command line arguments set, press the `Debug` button to run the program and have it work some magic!

## File Generation Notes

- PNG files generated from `-vtf` will be sent to a new folder on your desktop called `ExamineVTF`
- OBJ/MTL files generated from `-map` will be sent to a new folder on your desktop called `SourceMaps`
- OBJ/MTL files generated from `-model` will be sent to a new folder on your desktop called `SourceModels`
- RBXL/RBXM/PNG files generated from either `-map` or `-model` will be sent to `%localappdata%\Roblox Studio\content\source`.<br/>
As such, this assumes you are using the [Roblox Studio Mod Manager](https://github.com/CloneTrooper1019/Roblox-Studio-Mod-Manager)!
