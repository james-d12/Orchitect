#!/usr/bin/sh 

cd src/Orchitect.Engine.Persistence
dotnet ef migrations add $1

cd ../Orchitect.Inventory.Persistence
dotnet ef migrations add $1