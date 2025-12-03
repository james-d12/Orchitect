#!/usr/bin/sh 

cd src/Conductor.Engine.Persistence
dotnet ef migrations add $1