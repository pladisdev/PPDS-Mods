Make sure to check out the original mod which I forked to make this mod.
https://github.com/KitsueFox/PPDS-Mods

# Added Mod

## Duck Connect

Example use:
https://www.youtube.com/watch?v=bJL7ObEZeV8&t=125s

### Connects to PPDS with MQTT. Allows external control with other applications. 

* Quack duck
* Switch duck (each switch gives the name and id of the duck back)
* Ability to rename duck

### Running the example script

* Download an MQTT broker. Simplest one is Mosquitto (https://mosquitto.org/). I had issues reaching the website recently so I also recommend EMQX. Once the broker is running on localhost, port 1883 you can run the example script
* Follow the step for installation, then try running the duck_example.py script. You will need paho-mqtt which can be installed with `pip install paho-mqtt`. 

## Installation
To install these mods, you will need to install [MelonLoader](https://discord.gg/2Wn3N2P) (discord link, see \#how-to-install).
Then, you will have to put mod .dll file from [releases](https://github.com/pladisdev/PPDS-Mods/releases/tag/update-2023-02-27) into the `Mods` folder of your game directory
* You will also need a M2Mqtt.Net.dll to run the mod. 
* You can get it from here (https://www.nuget.org/packages/M2Mqtt/). 
* Donwload the package and open it (I used 7zip). In `<m2mqtt dir>\lib\net45` you can find a M2Mqtt.Net.dll file. 
* Place the dll in `<Placid Plastic Duck Simulator Instanll dir>\UserLibs`

## Building
To build these, drop required libraries (found in `<Placid Plastic Duck Simulator Instanll dir>\Placid Plastic Duck Simulator_Data\Managed` and both `MelonLoader.dll` `0Harmony.dll` from `<Placid Plastic Duck Simulator Instanll dir>\MelonLoader` after melonloader installation, 
list found in `Directory.Build.props`) into Libs folder, then use your IDE of choice to build. 
* You will also need M2Mqtt package from NuGet. v4.3 from Paolo. I used Visual Studio 2022 to get this package. You will get the same DLL file needed for installation.
* Libs folder is intended for newest libraries (MelonLoader 0.5.7)

## TODO
* Add description for each duckid, to better describe each duck for AI.
* Ability to move camera remotely.
* Intro screen control, automatically start instance
* Delete names remotely
* Better MQTT configuration
* Switch from MQTT to something simpler

