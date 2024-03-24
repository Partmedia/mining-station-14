.PHONY: build client server lint deploy noRL

DOTNET_FLAGS+= -c Release -v quiet -maxcpucount:5 /property:WarningLevel=0 /p:WarningsAsErrors=nullable
DOTNET_BUILD=dotnet build ${DOTNET_FLAGS}

build: noRL
	${DOTNET_BUILD}

rlbuild: libRL
	${DOTNET_BUILD}

fast: build fastserver fastclient

rlfast: rlbuild fastserver fastclient

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

lint: noRL
	${DOTNET_BUILD} Content.YAMLLinter
	cd bin/Content.YAMLLinter && ../../linklibs && ./Content.YAMLLinter

test:
	cd RobustToolbox/bin/UnitTesting && ../../../linklibs
	cd bin/Content.Tests && ../../linklibs
	dotnet test ${DOTNET_FLAGS}

package: noRL
	python3 Tools/package_server_build.py --hybrid-acz ${PACKAGE_BUILD_ARGS}

rlpackage: libRL RL
	python3 Tools/package_server_build.py --hybrid-acz ${PACKAGE_BUILD_ARGS}

deploy: package
	mv release/* ~ss14/downloads
	git push -f ms14 HEAD:ms/server

noRL:
	rm -f Content.Server/RL/libRL/libRL.so
