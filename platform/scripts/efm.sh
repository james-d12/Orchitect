#!/usr/bin/sh 

cd src/Conductor.Engine.Persistence
dotnet ef migrations add $1

cd ../Conductor.Inventory.Persistence
dotnet ef migrations add $1