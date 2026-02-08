mod command;
mod config;
mod login;
mod resource_template;

use crate::command::cli;
use crate::config::{handle_config_info, handle_config_setup};
use crate::login::handle_login;
use crate::resource_template::{get_resource_templates, handle_create_resource_template};

#[tokio::main]
async fn main() {
    let matches = cli().get_matches();

    match matches.subcommand() {
        Some(("login", sub_matches)) => {
            let email = sub_matches.get_one::<String>("email").map(|s| s.as_str());
            let password = sub_matches
                .get_one::<String>("password")
                .map(|s| s.as_str());

            handle_login(email, password).await.unwrap_or_else(|e| {
                eprintln!("Login failed: {}", e);
                std::process::exit(1);
            });
        }
        Some(("config", sub_matches)) => match sub_matches.subcommand() {
            Some(("setup", _)) => {
                handle_config_setup().unwrap_or_else(|e| {
                    eprintln!("Config setup failed: {}", e);
                    std::process::exit(1);
                });
            }
            Some(("info", _)) => {
                handle_config_info().unwrap_or_else(|e| {
                    eprintln!("Config info failed: {}", e);
                    std::process::exit(1);
                });
            }
            _ => unreachable!(),
        },
        Some(("resource-template", sub_matches)) => match sub_matches.subcommand() {
            Some(("get", _)) => {
                println!("Ran the resource template get sub command");
                let rts = get_resource_templates().await.unwrap();

                for rt in rts {
                    println!("Id: {0}", rt.id);
                    println!("Name: {0}", rt.name);
                    println!("Type: {0}", rt.resource_template_type);
                    println!("Description: {0}", rt.description);
                    println!("---------------------------------------");
                }
            }
            Some(("create", sub_matches)) => {
                let name = sub_matches.get_one::<String>("name").map(|s| s.as_str());
                let rt_type = sub_matches.get_one::<String>("type").map(|s| s.as_str());
                let description = sub_matches
                    .get_one::<String>("description")
                    .map(|s| s.as_str());
                let provider = sub_matches.get_one::<u8>("provider").copied();

                handle_create_resource_template(name, rt_type, description, provider)
                    .await
                    .unwrap_or_else(|e| {
                        eprintln!("Failed to create resource template: {}", e);
                        std::process::exit(1);
                    });
            }
            _ => unreachable!(),
        },
        Some(("application", _)) => {
            println!("Ran the application sub command");
        }
        Some(("environment", _)) => {
            println!("Ran the environment sub command");
        }
        Some(("organisation", _)) => {
            println!("Ran the organisation sub command");
        }
        _ => unreachable!(),
    }
}
