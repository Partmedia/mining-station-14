.PHONY: build client server lint deploy

DOTNET_FLAGS=-c Release -v quiet -maxcpucount:5 /property:WarningLevel=0
DOTNET_BUILD=dotnet build ${DOTNET_FLAGS}

build:
	${DOTNET_BUILD}

client:
	cd ./bin/Content.Client && ../../linklibs && ./Content.Client

server:
	cd ./bin/Content.Server && ./Content.Server --config-file ../../devel_config.toml

lint:
	${DOTNET_BUILD} Content.YAMLLinter
	cd bin/Content.YAMLLinter && ../../linklibs && ./Content.YAMLLinter

test:
	cd RobustToolbox/bin/UnitTesting && ../../../linklibs
	cd bin/Content.Tests && ../../linklibs
	dotnet test ${DOTNET_FLAGS}

package:
	python3 Tools/package_server_build.py --hybrid-acz

deploy: package
	mv release/* ~ss14/downloads
