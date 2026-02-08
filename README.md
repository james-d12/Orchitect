# Orchitect

Orchitect is a prototype self hosted Internal Developer Platform. It aims to be entirely self-hosted, and to provide a cohesive abstraction around managing applications and infrastructure.  

It is currently a multi language monorepo, consisting of 3 main components: 

- Platform: This is the heart of Orchitect, it has all of the business logic, api, and persistence code.
- CLI Portal: This is a rust CLI tool that allows you to interact with Orchitect's API through an easy to use CLI.
- Web Portal: This is a Typescript based Web Portal that allows you to interact with Orchitect's API.

# Getting Started

## Requirements

- [Docker](https://www.docker.com/)
- [Rust](https://rust-lang.org/tools/install/)
- [Dotnet-10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Nodejs](https://nodejs.org/en)
- [Pnpm](https://pnpm.io/installation)

## Platform

You need to have [Docker](https://www.docker.com/)
and [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed.

1. You will need to clone the repository from the master branch.
2. Next you will need to go the ```platform``` sub-folder.
3. Now, run the ```setup.sh``` script inside the ```./scripts``` folder. This will install the required global dotnet
   tools like efcore.
4. Next you can run the Aspire project, which will spin up the required Databases and services, so you can browse locally.

# License

Orchitect is licensed under the [AGPLv3 License](./LICENSE.md).