use clap::Command;
use serde::{Deserialize, Serialize};
use std::io::{self, Write};

#[derive(Debug, Serialize, Deserialize)]
pub struct LoginRequest {
    pub email: String,
    pub password: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct LoginResponse {
    #[serde(rename = "accessToken")]
    pub access_token: String,
}

pub fn get_login_command() -> Command {
    Command::new("login")
        .about("Login to Orchitect and save access token")
        .arg(
            clap::Arg::new("email")
                .short('e')
                .long("email")
                .help("Email address")
                .value_name("EMAIL")
                .required(false),
        )
        .arg(
            clap::Arg::new("password")
                .short('p')
                .long("password")
                .help("Password")
                .value_name("PASSWORD")
                .required(false),
        )
}

pub async fn handle_login(
    email: Option<&str>,
    password: Option<&str>,
) -> Result<(), Box<dyn std::error::Error>> {
    // Get email and password from arguments or prompt
    let email = if let Some(e) = email {
        e.to_string()
    } else {
        print!("Enter email: ");
        io::stdout().flush()?;
        let mut input = String::new();
        io::stdin().read_line(&mut input)?;
        input.trim().to_string()
    };

    let password = if let Some(p) = password {
        p.to_string()
    } else {
        print!("Enter password: ");
        io::stdout().flush()?;
        let mut input = String::new();
        io::stdin().read_line(&mut input)?;
        input.trim().to_string()
    };

    if email.is_empty() || password.is_empty() {
        return Err("Email and password cannot be empty".into());
    }

    // Get API URL from config
    let api_url = crate::config::get_api_url()?;

    // Make login request
    let client = reqwest::Client::new();
    let login_url = format!("{}/users/login", api_url);

    let request_body = LoginRequest { email, password };

    let response = client.post(&login_url).json(&request_body).send().await?;

    if !response.status().is_success() {
        return Err(format!("Login failed with status: {}", response.status()).into());
    }

    let login_response: LoginResponse = response.json().await?;

    // Save access token to config
    let mut config = crate::config::load_config()?;
    config.access_token = Some(login_response.access_token);
    crate::config::save_config(&config)?;

    println!("Login successful! Access token saved.");
    Ok(())
}
