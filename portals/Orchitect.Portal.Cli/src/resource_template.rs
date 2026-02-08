use crate::config::get_api_url;
use clap::{Arg, Command};
use reqwest::{Error, StatusCode};
use serde::{Deserialize, Serialize};
use std::io::{self, Write};

#[derive(Debug, Deserialize, PartialEq)]
pub struct ResourceTemplate {
    pub id: String,
    pub name: String,
    #[serde(rename = "type")]
    pub resource_template_type: String,
    pub description: String,
}

#[derive(Debug, Deserialize)]
pub struct GetResourceTemplatesResponse {
    #[serde(rename = "resourceTemplates")]
    pub resource_templates: Vec<ResourceTemplate>,
}

#[derive(Debug, Serialize)]
pub struct CreateResourceTemplateRequest {
    pub name: String,
    #[serde(rename = "type")]
    pub resource_template_type: String,
    pub description: String,
    pub provider: u8,
}

pub fn get_resource_template_command() -> Command {
    Command::new("resource-template")
        .about("Manage Resource Templates")
        .alias("rt")
        .subcommand_required(true)
        .arg_required_else_help(true)
        .allow_external_subcommands(true)
        .subcommand(Command::new("get").about("Gets Resource Templates"))
        .subcommand(Command::new("delete").about("Delete a Resource Template"))
        .subcommand(
            Command::new("create")
                .about("Creates a new Resource Template")
                .arg(
                    Arg::new("name")
                        .long("name")
                        .help("Name of the resource template")
                        .value_name("NAME"),
                )
                .arg(
                    Arg::new("type")
                        .long("type")
                        .help("Type of the resource template")
                        .value_name("TYPE"),
                )
                .arg(
                    Arg::new("description")
                        .long("description")
                        .help("Description of the resource template")
                        .value_name("DESCRIPTION"),
                )
                .arg(
                    Arg::new("provider")
                        .long("provider")
                        .help("Provider ID (0-255)")
                        .value_name("PROVIDER")
                        .value_parser(clap::value_parser!(u8)),
                ),
        )
}

pub async fn get_resource_templates() -> Result<Vec<ResourceTemplate>, Error> {
    let api_url = get_api_url().expect("Failed to load API URL. Please run 'cdr config' first.");
    let url = format!("{}/resource-templates", api_url);
    let body = reqwest::get(&url).await?.text().await?;
    let resource_templates: GetResourceTemplatesResponse = serde_json::from_str(&body).unwrap();
    Ok(resource_templates.resource_templates)
}

pub async fn create_resource_template(request: CreateResourceTemplateRequest) -> Result<(), Error> {
    let api_url = get_api_url().expect("Failed to load API URL. Please run 'cdr config' first.");
    let url = format!("{}/resource-templates", api_url);
    let client = reqwest::Client::new();
    let response = client.post(&url).json(&request).send().await?;

    let status = response.status();

    if status == StatusCode::OK {
        println!("Created resource template \"{}\"", request.name);
        Ok(())
    } else {
        Err(response.error_for_status().unwrap_err())
    }
}

fn prompt_for_string(prompt: &str) -> io::Result<String> {
    print!("{}", prompt);
    io::stdout().flush()?;
    let mut input = String::new();
    io::stdin().read_line(&mut input)?;
    Ok(input.trim().to_string())
}

fn prompt_for_u8(prompt: &str) -> io::Result<u8> {
    loop {
        let input = prompt_for_string(prompt)?;
        match input.parse::<u8>() {
            Ok(value) => return Ok(value),
            Err(_) => println!("Invalid input. Please enter a number between 0 and 255."),
        }
    }
}

pub async fn handle_create_resource_template(
    name: Option<&str>,
    rt_type: Option<&str>,
    description: Option<&str>,
    provider: Option<u8>,
) -> Result<(), Box<dyn std::error::Error>> {
    // If all arguments are provided, use them directly
    if let (Some(n), Some(t), Some(d), Some(p)) = (name, rt_type, description, provider) {
        let request = CreateResourceTemplateRequest {
            name: n.to_string(),
            resource_template_type: t.to_string(),
            description: d.to_string(),
            provider: p,
        };
        return create_resource_template(request)
            .await
            .map_err(|e| e.into());
    }

    // Otherwise, prompt the user for each value
    let name = if let Some(n) = name {
        n.to_string()
    } else {
        prompt_for_string("Enter name: ")?
    };

    let rt_type = if let Some(t) = rt_type {
        t.to_string()
    } else {
        prompt_for_string("Enter type: ")?
    };

    let description = if let Some(d) = description {
        d.to_string()
    } else {
        prompt_for_string("Enter description: ")?
    };

    let provider = if let Some(p) = provider {
        p
    } else {
        prompt_for_u8("Enter provider (0-255): ")?
    };

    let request = CreateResourceTemplateRequest {
        name,
        resource_template_type: rt_type,
        description,
        provider,
    };

    create_resource_template(request)
        .await
        .map_err(|e| e.into())
}
