# Source2Roblox

## ⚠️ NO LONGER BEING MAINTAINED ⚠️
This project is no longer being maintained due to misuse by bad actors. While you are free to continue using this project as is, I will not be providing any support or assistance in getting it working. This is intended as a reference and showcase of technical feat, nothing more. Do not use this code to rip levels for use in monetized experiences without explicit permission to use them on Roblox.

<hr/>

Source2Roblox is a super awesome C# console application that can:
- Rip data from Valve's game files (*.VPK, *.VMT, *.VTF, *.BSP)
- Compile that data into files that Roblox can work with (*.RBXM, *.RBXL, *.MESH, *.PNG)

# Usage

This program is not user-friendly and will not be made easier to use.<br/> But if you're feeling brave enough, you can set it up with the following steps:

1. You'll need to install the [Roblox Studio Mod Manager](https://github.com/MaximumADHD/Roblox-Studio-Mod-Manager) since the program currently targets `%localappdata%\Roblox Studio\content` for deploying local files.
2. Install Visual Studio 2019 with `Visual C#` and `.NET Framework 4.7.2`
3. Clone the following GitHub repositories into a single directory on your file system:
   - https://github.com/MaximumADHD/Source2Roblox
   - https://github.com/MaximumADHD/ValveKeyValue
   - https://github.com/MaximumADHD/Roblox-File-Format
4. Open the solution file `Source2Roblox.sln`
5. Right click on the `Source2Roblox` project and click Properties
6. Navigate to the `Debug` tab, and use some of the following command line arguments to get things up and running:

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
- RBXL/RBXM/PNG files generated from either `-map` or `-model` will be sent to `%localappdata%\Roblox Studio\content\source`.
