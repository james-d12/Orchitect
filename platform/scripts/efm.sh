#!/usr/bin/sh 

cd src/Orchitect.Core.Persistence
dotnet ef migrations add $1

cd ../Orchitect.Engine.Persistence
dotnet ef migrations add $1

cd ../Orchitect.Inventory.Persistence
dotnet ef migrations add $1