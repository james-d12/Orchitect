use clap::Command;
use serde::{Deserialize, Serialize};
use std::fs;
use std::io::{self, Write};
use std::path::PathBuf;

#[derive(Debug, Serialize, Deserialize)]
pub struct Config {
    pub api_url: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub access_token: Option<String>,
}

#[cfg(target_os = "windows")]
fn get_config_dir() -> PathBuf {
    let app_data = std::env::var("APPDATA").expect("Failed to get APPDATA environment variable");
    PathBuf::from(app_data).join("cdr")
}

#[cfg(target_os = "linux")]
fn get_config_dir() -> PathBuf {
    let home = std::env::var("HOME").expect("Failed to get HOME environment variable");
    PathBuf::from(home).join(".config").join("cdr")
}

#[cfg(target_os = "macos")]
fn get_config_dir() -> PathBuf {
    let home = std::env::var("HOME").expect("Failed to get HOME environment variable");
    PathBuf::from(home).join(".config").join("cdr")
}

pub fn get_config_file() -> PathBuf {
    get_config_dir().join("config.json")
}

pub fn load_config() -> Result<Config, Box<dyn std::error::Error>> {
    let config_file = get_config_file();
    let content = fs::read_to_string(&config_file)?;
    let config: Config = serde_json::from_str(&content)?;
    Ok(config)
}

pub fn save_config(config: &Config) -> Result<(), Box<dyn std::error::Error>> {
    let config_dir = get_config_dir();
    fs::create_dir_all(&config_dir)?;

    let config_file = get_config_file();
    let content = serde_json::to_string_pretty(config)?;
    fs::write(&config_file, content)?;
    Ok(())
}

pub fn get_api_url() -> Result<String, Box<dyn std::error::Error>> {
    let config = load_config()?;
    Ok(config.api_url)
}

pub fn get_config_command() -> Command {
    Command::new("config")
        .about("Configure API settings")
        .subcommand_required(true)
        .arg_required_else_help(true)
        .subcommand(Command::new("setup").about("Setup API configuration"))
        .subcommand(Command::new("info").about("Display current API configuration"))
}

pub fn handle_config_setup() -> Result<(), Box<dyn std::error::Error>> {
    let mut url = String::new();

    // Check if config already exists
    if let Ok(config) = load_config() {
        println!("Current API URL: {}", config.api_url);
        print!("Enter new API URL (leave blank to keep current): ");
        io::stdout().flush()?;

        io::stdin().read_line(&mut url)?;
        let url = url.trim();

        let final_url = if url.is_empty() {
            config.api_url
        } else {
            url.to_string()
        };

        let new_config = Config {
            api_url: final_url,
            access_token: config.access_token,
        };
        save_config(&new_config)?;
    } else {
        print!("Enter API URL: ");
        io::stdout().flush()?;

        io::stdin().read_line(&mut url)?;
        let url = url.trim();

        if url.is_empty() {
            return Err("API URL cannot be empty".into());
        }

        let config = Config {
            api_url: url.to_string(),
            access_token: None,
        };
        save_config(&config)?;
    }

    println!("Configuration saved successfully!");
    Ok(())
}

pub fn handle_config_info() -> Result<(), Box<dyn std::error::Error>> {
    match load_config() {
        Ok(config) => {
            let config_file_path = get_config_file();
            let config_file = config_file_path.to_str().unwrap();
            println!("Current API Configuration:");
            println!("File: {}", config_file);
            println!("Values: ");
            println!("  API URL: {}", config.api_url);
            println!(
                "  Access Token: {}",
                config.access_token.unwrap_or_default()
            );
            Ok(())
        }
        Err(_) => Err("No configuration found. Please run 'cdr config setup' first.".into()),
    }
}
