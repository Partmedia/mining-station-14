Mining Station 14
=================
You and your friends racked up too much poker debt and must now mine to make
your way back.

Play
----
You do not need this repository to play Mining Station 14. Instead, `download
the Space Station 14 Launcher <https://spacestation14.io/about/nightlies/>`_
and connect to the **Mining Station 14** server from the launcher.

Compiling
---------
You will need to download and install:

- Git
- .NET 7 SDK

After cloning this repository, run::

    git submodule update --init --recursive

Note that unlike upstream Space Station 14, this will not run automatically.

.. Note:: Unless you are Mining Station 14 staff, **this command will fail to
   clone Mining Station 14 resources, including maps**. You will still be able
   to run the server and create maps without Mining Station 14 resources.

Compile Mining Station 14 using the command::

    dotnet build

To start the server::

    dotnet run --project Content.Server

To start the client::

    dotnet run --project Content.Client

License
-------
Unless otherwise specified, source code for Mining Station 14 is available
under the `MIT License <LICENSE.TXT>`_. Notable exceptions are for:

- Sprites, whose licenses are noted in their *meta.json* descriptions; and the
- Mining Station 14 resources, which are not available to the general public at this time.
