.PHONY: setup dev server web test build serve

setup:
	cd server && dotnet restore && dotnet tool restore
	cd web && npm install

server:
	cd server && dotnet run --project Kirie.Api

web:
	cd web && npm run dev

# Both, stopped together with Ctrl+C.
dev:
	@echo "server  http://localhost:5080"
	@echo "web     http://localhost:5173"
	@$(MAKE) -j2 server web

test:
	cd server && dotnet test
	cd web && npm run check

build:
	cd web && npm run build

# One process: the server hosting the built book.
serve: build
	cd server && dotnet run --project Kirie.Api -- --Kirie:Web="$(CURDIR)/web/build"
