# Conductor

Conductor is a prototype self hosted Internal Developer Platform. It aims to be entirely self-hosted, and to provide a cohesive abstraction around managing applications and infrastructure.  

It is currently a multi language monorepo, consisting of 3 main components: 

- Engine: This is the heart of Conductor, it has all of the business logic, api, and persistence code.
- CLI: This is a rust CLI tool that allows you to interact with Conductor's API through an easy to use CLI.
- Web: This is a Typescript based Web Portal that allows you to interact with Conductor's API.

[![Engine Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=personal-james_conductor-engine&metric=alert_status&token=c14734eb1fea1906d0a1b4d7796d59f34dd9f661)](https://sonarcloud.io/summary/new_code?id=personal-james_conductor-engine)

[![CLI Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=personal-james_conductor-cli&metric=alert_status&token=667e1a2c2d857e82f8ca39f191475b2b0f2b15b6)](https://sonarcloud.io/summary/new_code?id=personal-james_conductor-cli)
 
[![Web Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=personal-james_conductor-web&metric=alert_status&token=38430168f73055fa515f93060746e7806b1195b1)](https://sonarcloud.io/summary/new_code?id=personal-james_conductor-web)

# Getting Started

## Requirements

- [Docker](https://www.docker.com/)
- [Rust](https://rust-lang.org/tools/install/)
- [Dotnet-10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Nodejs](https://nodejs.org/en)
- [Pnpm](https://pnpm.io/installation)

## Engine

You need to have [Docker](https://www.docker.com/)
and [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed.

1. You will need to clone the repository from the master branch.
2. Next you will need to go the ```engine``` sub-folder.
3. Now, run the ```setup.sh``` script inside the ```./scripts``` folder. This will install the required global dotnet
   tools like efcore.
4. Next you can run the docker compose to spin up the API locally.

# License

Conductor is licensed under the [GPLV3 License](./LICENSE.md).