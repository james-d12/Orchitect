use crate::config::get_config_command;
use crate::login::get_login_command;
use crate::resource_template::get_resource_template_command;
use clap::Command;

pub fn get_application_command() -> Command {
    Command::new("application")
        .about("Manage Applications")
        .alias("app")
        .subcommand_required(true)
        .arg_required_else_help(true)
        .allow_external_subcommands(true)
        .subcommand(Command::new("get").about("Gets Applications"))
        .subcommand(Command::new("delete").about("Delete an Application"))
        .subcommand(Command::new("create").about("Creates a new Application"))
}

pub fn get_environment_command() -> Command {
    Command::new("environment")
        .about("Manage Environments")
        .alias("env")
        .subcommand_required(true)
        .arg_required_else_help(true)
        .allow_external_subcommands(true)
        .subcommand(Command::new("get").about("Gets Environments"))
        .subcommand(Command::new("delete").about("Delete an Environment"))
        .subcommand(Command::new("create").about("Creates a new Environment"))
}

pub fn get_organisation_command() -> Command {
    Command::new("organisation")
        .about("Manage Organisations")
        .alias("org")
        .subcommand_required(true)
        .arg_required_else_help(true)
        .allow_external_subcommands(true)
        .subcommand(Command::new("get").about("Gets Organisations"))
        .subcommand(Command::new("delete").about("Delete an Organisation"))
        .subcommand(Command::new("create").about("Creates a new Organisation"))
}

pub fn cli() -> Command {
    Command::new("cdr")
        .about("Manage Conductor through the Cli")
        .subcommand_required(true)
        .arg_required_else_help(true)
        .allow_external_subcommands(true)
        .subcommand(get_login_command())
        .subcommand(get_config_command())
        .subcommand(get_resource_template_command())
        .subcommand(get_application_command())
        .subcommand(get_environment_command())
        .subcommand(get_organisation_command())
}
