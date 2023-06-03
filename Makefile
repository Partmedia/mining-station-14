.PHONY: build client server lint deploy

DOTNET_FLAGS=-c Release -v quiet -maxcpucount:5 /property:WarningLevel=0
DOTNET_BUILD=dotnet build ${DOTNET_FLAGS}

fast: build fastserver fastclient

build: libRL
	${DOTNET_BUILD}

libRL:
	cd Content.Server/RL/libRL && make

RL:
	cd Resources/Mining/RL && make

client:
	cd ./bin/Content.Client && ../../linklibs && ./Content.Client

fastclient:
	cd ./bin/Content.Client && ../../linklibs && ./Content.Client --connect-address localhost:1211 --connect && pkill -TERM Content.Server

server:
	cd ./bin/Content.Server && ./Content.Server --config-file ../../devel_config.toml

fastserver:
	cd ./bin/Content.Server && ./Content.Server --config-file ../../fast_config.toml &

lint:
	rm -f Content.Server/RL/libRL/libRL.so
	${DOTNET_BUILD} Content.YAMLLinter
	cd bin/Content.YAMLLinter && ../../linklibs && ./Content.YAMLLinter

test:
	cd RobustToolbox/bin/UnitTesting && ../../../linklibs
	cd bin/Content.Tests && ../../linklibs
	dotnet test ${DOTNET_FLAGS}

package: libRL RL
	python3 Tools/package_server_build.py --hybrid-acz

deploy: package
	mv release/* ~ss14/downloads
