Mining Station / Dungeon Dig 14
===============================
This is the combined source repository for Mining Station and Dungeon Dig 14.

Play
----
You do not need this repository to play Mining Station 14. Instead, `download
the Space Station 14 Launcher <https://spacestation14.io/about/nightlies/>`_
and connect to the **Mining Station 14** server from the launcher.

Compiling
---------
1. Download and install:

   - Git
   - .NET 7 SDK

2. Clone this repository.

3. Inside the newly cloned respository, run::

    git submodule update --init --recursive

   Note that unlike upstream Space Station 14, this will not run automatically.

4. Compile Mining Station 14 using the command::

    dotnet build

5. To start the server::

    dotnet run --project Content.Server

   To start the client::

    dotnet run --project Content.Client

Dungeons Not Included
---------------------
This repository contains all of the client and server source code, assets, and
data necessary to develop and run the game. However, it does not include the
maps, asteroids, and dungeons. You are more than welcome to start your own
server using this code, as long as you follow the terms of our license.
However, you will have to develop your own maps.

License
-------
Unless otherwise specified, source code for Mining Station 14 is available
under the `MIT License <LICENSE.TXT>`_. Notable exceptions are for:

- Sprites, whose licenses are noted in their *meta.json* descriptions
